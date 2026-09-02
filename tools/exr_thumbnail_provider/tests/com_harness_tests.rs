use std::fs::File;
use std::io::Read;
use std::path::PathBuf;
use std::ptr;
use windows::core::Interface;
use windows::Win32::Foundation::E_POINTER;
use windows::Win32::Graphics::Gdi::{DeleteObject, HBITMAP};
use windows::Win32::System::Com::{CoInitializeEx, CoUninitialize, COINIT_MULTITHREADED};
use windows::Win32::UI::Shell::PropertiesSystem::IInitializeWithStream;
use windows::Win32::UI::Shell::{
    IThumbnailProvider, SHCreateMemStream, WTSAT_ARGB, WTS_ALPHATYPE,
};

use exr_thumbnail_provider::com::ThumbnailProvider;

fn get_scratch_dir() -> PathBuf {
    let mut p = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    p.pop();
    p.pop();
    p.push("scratch");
    p
}

#[test]
fn test_com_harness_end_to_end() {
    unsafe {
        let _ = CoInitializeEx(None, COINIT_MULTITHREADED);
    }

    let provider = ThumbnailProvider::new();
    let init_iface: IInitializeWithStream = provider.into();
    let thumb_iface: IThumbnailProvider = init_iface.cast().expect("cast to IThumbnailProvider failed");

    // 1. Uninitialized GetThumbnail should fail
    let mut hbmp = HBITMAP::default();
    let mut alpha = WTS_ALPHATYPE::default();
    let res = unsafe { thumb_iface.GetThumbnail(128, &mut hbmp, &mut alpha) };
    assert!(res.is_err(), "GetThumbnail before Initialize must fail");

    // 2. Null pointers should return E_POINTER
    let res_null = unsafe { thumb_iface.GetThumbnail(128, ptr::null_mut(), &mut alpha) };
    assert_eq!(res_null.unwrap_err().code(), E_POINTER);

    // 3. Load toon_light.exr into an IStream via SHCreateMemStream
    let path = get_scratch_dir().join("toon_light.exr");
    let mut file = File::open(&path).expect("failed to open toon_light.exr");
    let mut bytes = Vec::new();
    file.read_to_end(&mut bytes).expect("read failed");

    let stream = unsafe {
        SHCreateMemStream(Some(&bytes)).expect("SHCreateMemStream failed")
    };

    // 4. Initialize provider with stream
    unsafe {
        init_iface.Initialize(&stream, 0).expect("Initialize failed");
    }

    // 5. Test GetThumbnail (256)
    let mut hbmp1 = HBITMAP::default();
    let mut alpha1 = WTS_ALPHATYPE::default();
    unsafe {
        thumb_iface.GetThumbnail(256, &mut hbmp1, &mut alpha1).expect("GetThumbnail 256 failed");
    }
    assert!(!hbmp1.is_invalid(), "HBITMAP 1 must be valid");
    assert_eq!(alpha1, WTSAT_ARGB, "Alpha type must be WTSAT_ARGB");

    // Clean up HBITMAP 1
    unsafe {
        let _ = DeleteObject(hbmp1);
    }

    // 6. Test repeated GetThumbnail (96) on the same instance
    let mut hbmp2 = HBITMAP::default();
    let mut alpha2 = WTS_ALPHATYPE::default();
    unsafe {
        thumb_iface.GetThumbnail(96, &mut hbmp2, &mut alpha2).expect("Repeated GetThumbnail 96 failed");
    }
    assert!(!hbmp2.is_invalid(), "HBITMAP 2 must be valid");
    assert_eq!(alpha2, WTSAT_ARGB, "Alpha type must be WTSAT_ARGB");

    // Clean up HBITMAP 2
    unsafe {
        let _ = DeleteObject(hbmp2);
        CoUninitialize();
    }
}
