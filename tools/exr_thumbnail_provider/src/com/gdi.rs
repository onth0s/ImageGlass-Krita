//! Windows GDI DIB section allocation and BGRA buffer mapping.

use std::ptr;
use windows::core::Result;
use windows::Win32::Foundation::{E_INVALIDARG, E_OUTOFMEMORY};
use windows::Win32::Graphics::Gdi::{
    CreateDIBSection, DeleteObject, BITMAPINFO, BITMAPINFOHEADER, BI_RGB, DIB_RGB_COLORS, HBITMAP,
    HDC,
};

pub fn create_dib_section(width: u32, height: u32, bgra_premultiplied: &[u8]) -> Result<HBITMAP> {
    if width == 0 || height == 0 {
        return Err(windows::core::Error::from_hresult(E_INVALIDARG));
    }

    let expected_bytes = (width as usize)
        .checked_mul(height as usize)
        .and_then(|px| px.checked_mul(4))
        .ok_or_else(|| windows::core::Error::from_hresult(E_INVALIDARG))?;

    if bgra_premultiplied.len() < expected_bytes {
        return Err(windows::core::Error::from_hresult(E_INVALIDARG));
    }

    let bmi = BITMAPINFO {
        bmiHeader: BITMAPINFOHEADER {
            biSize: std::mem::size_of::<BITMAPINFOHEADER>() as u32,
            biWidth: width as i32,
            biHeight: -(height as i32), // Negative height specifies a top-down DIB
            biPlanes: 1,
            biBitCount: 32,
            biCompression: BI_RGB.0,
            biSizeImage: 0,
            biXPelsPerMeter: 0,
            biYPelsPerMeter: 0,
            biClrUsed: 0,
            biClrImportant: 0,
        },
        bmiColors: [windows::Win32::Graphics::Gdi::RGBQUAD::default()],
    };

    let mut bits_ptr: *mut std::ffi::c_void = ptr::null_mut();

    let hbitmap = unsafe {
        CreateDIBSection(
            HDC::default(),
            &bmi,
            DIB_RGB_COLORS,
            &mut bits_ptr,
            None,
            0,
        )
    }?;

    if hbitmap.is_invalid() || bits_ptr.is_null() {
        return Err(windows::core::Error::from_hresult(E_OUTOFMEMORY));
    }

    // Copy premultiplied BGRA pixels into the DIBSection memory
    unsafe {
        ptr::copy_nonoverlapping(
            bgra_premultiplied.as_ptr(),
            bits_ptr as *mut u8,
            expected_bytes,
        );
    }

    Ok(hbitmap)
}

/// Safely destroys an HBITMAP handle if an error occurs.
pub fn delete_hbitmap_safe(hbitmap: HBITMAP) {
    if !hbitmap.is_invalid() {
        unsafe {
            let _ = DeleteObject(hbitmap);
        }
    }
}
