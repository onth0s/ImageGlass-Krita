//! COM IInitializeWithStream and IThumbnailProvider implementations.

use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Mutex;
use windows::core::{implement, HRESULT};
use windows::Win32::Foundation::{E_FAIL, E_HANDLE, E_INVALIDARG, E_POINTER};
use windows::Win32::Graphics::Gdi::HBITMAP;
use windows::Win32::System::Com::{IStream, STREAM_SEEK_SET};
use windows::Win32::UI::Shell::PropertiesSystem::IInitializeWithStream_Impl;
use windows::Win32::UI::Shell::{
    IThumbnailProvider_Impl, WTSAT_ARGB, WTSAT_UNKNOWN, WTS_ALPHATYPE,
};

use super::gdi::create_dib_section;
use super::stream_adapter::StreamAdapter;
use crate::core::decode_and_generate_thumbnail;

pub static OBJECT_COUNT: AtomicUsize = AtomicUsize::new(0);

#[implement(windows::Win32::UI::Shell::PropertiesSystem::IInitializeWithStream, windows::Win32::UI::Shell::IThumbnailProvider)]
pub struct ThumbnailProvider {
    stream: Mutex<Option<IStream>>,
}

impl ThumbnailProvider {
    pub fn new() -> Self {
        OBJECT_COUNT.fetch_add(1, Ordering::SeqCst);
        Self {
            stream: Mutex::new(None),
        }
    }
}

impl Drop for ThumbnailProvider {
    fn drop(&mut self) {
        OBJECT_COUNT.fetch_sub(1, Ordering::SeqCst);
    }
}

impl IInitializeWithStream_Impl for ThumbnailProvider_Impl {
    fn Initialize(&self, pstream: Option<&IStream>, _grfmode: u32) -> windows::core::Result<()> {
        let stream = pstream.ok_or_else(|| windows::core::Error::from_hresult(E_INVALIDARG))?;

        let mut guard = self
            .stream
            .lock()
            .map_err(|_| windows::core::Error::from_hresult(E_HANDLE))?;

        *guard = Some(stream.clone());
        Ok(())
    }
}

impl IThumbnailProvider_Impl for ThumbnailProvider_Impl {
    fn GetThumbnail(
        &self,
        cx: u32,
        phbmp: *mut HBITMAP,
        pdwalpha: *mut WTS_ALPHATYPE,
    ) -> windows::core::Result<()> {
        if phbmp.is_null() || pdwalpha.is_null() {
            return Err(windows::core::Error::from_hresult(E_POINTER));
        }

        // Initialize outputs safely
        unsafe {
            *phbmp = HBITMAP::default();
            *pdwalpha = WTSAT_UNKNOWN;
        }

        // Acquire stream clone and release mutex immediately
        let stream = {
            let guard = self
                .stream
                .lock()
                .map_err(|_| windows::core::Error::from_hresult(E_HANDLE))?;
            guard
                .as_ref()
                .cloned()
                .ok_or_else(|| windows::core::Error::from_hresult(E_FAIL))?
        };

        // Reset stream to offset 0
        unsafe {
            stream
                .Seek(0, STREAM_SEEK_SET, None)
                .map_err(|e| windows::core::Error::new(e.code(), format!("Seek failed: {:?}", e)))?;
        }

        let mut adapter = StreamAdapter::new(stream);

        // Run pure Rust decode & processing pipeline (default exposure = 0.0)
        let thumbnail = decode_and_generate_thumbnail(&mut adapter, cx, 0.0)
            .map_err(|e| {
                windows::core::Error::new(
                    HRESULT(0x80004005u32 as i32), // E_FAIL
                    format!("EXR thumbnail pipeline error: {}", e),
                )
            })?;

        // Allocate Windows GDI DIBSection
        let hbitmap = create_dib_section(
            thumbnail.width,
            thumbnail.height,
            &thumbnail.bgra_premultiplied,
        )?;

        // Output results and transfer ownership to Explorer
        unsafe {
            *phbmp = hbitmap;
            *pdwalpha = WTSAT_ARGB;
        }

        Ok(())
    }
}
