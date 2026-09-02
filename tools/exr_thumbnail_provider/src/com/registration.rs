//! COM Server registration and registry management for Windows Explorer.

use windows::core::GUID;
use windows::Win32::Foundation::HMODULE;
use windows::Win32::System::LibraryLoader::GetModuleFileNameW;
use windows::Win32::System::Registry::{
    RegCloseKey, RegCreateKeyExW, RegDeleteTreeW, RegSetValueExW, HKEY,
    HKEY_CLASSES_ROOT, KEY_ALL_ACCESS, REG_OPTION_NON_VOLATILE, REG_SZ,
};
use windows::Win32::UI::Shell::{SHChangeNotify, SHCNE_ASSOCCHANGED, SHCNF_IDLIST};

pub const CLSID_EXR_THUMBNAIL_PROVIDER: GUID =
    GUID::from_u128(0xb92c3d5e_7840_4a1e_8b39_44f4c1b1e019);

pub const CLSID_STR: &str = "{B92C3D5E-7840-4A1E-8B39-44F4C1B1E019}";
pub const THUMBNAIL_PROVIDER_SHELLEX_GUID: &str = "{e357fccd-a995-4576-b01f-234630154e96}";

fn to_wide(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

fn set_registry_string(hkey: HKEY, subkey: &str, value_name: Option<&str>, value: &str) -> bool {
    let mut key: HKEY = HKEY::default();
    let subkey_w = to_wide(subkey);

    let status = unsafe {
        RegCreateKeyExW(
            hkey,
            windows::core::PCWSTR(subkey_w.as_ptr()),
            0,
            windows::core::PCWSTR::null(),
            REG_OPTION_NON_VOLATILE,
            KEY_ALL_ACCESS,
            None,
            &mut key,
            None,
        )
    };

    if status.is_err() {
        return false;
    }

    let val_w = to_wide(value);
    let val_bytes = unsafe {
        std::slice::from_raw_parts(val_w.as_ptr() as *const u8, val_w.len() * 2)
    };

    let val_name_w = value_name.map(to_wide);
    let val_name_ptr = val_name_w.as_ref().map(|v| v.as_ptr()).unwrap_or(std::ptr::null());

    let set_status = unsafe {
        RegSetValueExW(
            key,
            windows::core::PCWSTR(val_name_ptr),
            0,
            REG_SZ,
            Some(val_bytes),
        )
    };

    unsafe {
        let _ = RegCloseKey(key);
    }

    set_status.is_ok()
}

pub fn register_server(hinstance: HMODULE) -> windows::core::Result<()> {
    let mut dll_path = [0u16; 1024];
    let len = unsafe { GetModuleFileNameW(hinstance, &mut dll_path) };
    if len == 0 {
        return Err(windows::core::Error::from_win32());
    }
    let dll_path_str = String::from_utf16_lossy(&dll_path[..len as usize]);

    // 1. HKCR\CLSID\{CLSID} = "ImageGlass OpenEXR Thumbnail Provider"
    let clsid_key = format!("CLSID\\{}", CLSID_STR);
    set_registry_string(
        HKEY_CLASSES_ROOT,
        &clsid_key,
        None,
        "ImageGlass OpenEXR Thumbnail Provider",
    );

    // 2. HKCR\CLSID\{CLSID}\InprocServer32 = <path_to_dll>
    let inproc_key = format!("CLSID\\{}\\InprocServer32", CLSID_STR);
    set_registry_string(HKEY_CLASSES_ROOT, &inproc_key, None, &dll_path_str);
    set_registry_string(
        HKEY_CLASSES_ROOT,
        &inproc_key,
        Some("ThreadingModel"),
        "Apartment",
    );

    // 3. HKCR\.exr\ShellEx\{e357fccd-a995-4576-b01f-234630154e96} = {CLSID}
    let shellex_key = format!(".exr\\ShellEx\\{}", THUMBNAIL_PROVIDER_SHELLEX_GUID);
    set_registry_string(HKEY_CLASSES_ROOT, &shellex_key, None, CLSID_STR);

    // Notify Windows Explorer to refresh association caches
    unsafe {
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, None, None);
    }

    Ok(())
}

pub fn unregister_server() -> windows::core::Result<()> {
    // 1. Delete .exr\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}
    let shellex_key = format!(".exr\\ShellEx\\{}", THUMBNAIL_PROVIDER_SHELLEX_GUID);
    let shellex_w = to_wide(&shellex_key);
    unsafe {
        let _ = RegDeleteTreeW(HKEY_CLASSES_ROOT, windows::core::PCWSTR(shellex_w.as_ptr()));
    }

    // 2. Delete CLSID\{CLSID}
    let clsid_key = format!("CLSID\\{}", CLSID_STR);
    let clsid_w = to_wide(&clsid_key);
    unsafe {
        let _ = RegDeleteTreeW(HKEY_CLASSES_ROOT, windows::core::PCWSTR(clsid_w.as_ptr()));
    }

    // Notify Windows Explorer to refresh association caches
    unsafe {
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, None, None);
    }

    Ok(())
}
