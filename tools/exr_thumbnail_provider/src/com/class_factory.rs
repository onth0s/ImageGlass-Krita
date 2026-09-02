//! COM IClassFactory implementation.

use std::ffi::c_void;
use std::sync::atomic::Ordering;
use windows::core::{implement, GUID, Interface};
use windows::Win32::Foundation::{
    BOOL, CLASS_E_NOAGGREGATION, E_NOINTERFACE, E_POINTER,
};
use windows::Win32::System::Com::{IClassFactory, IClassFactory_Impl};

use super::thumbnail_provider::{ThumbnailProvider, OBJECT_COUNT};

#[implement(IClassFactory)]
pub struct ClassFactory;

impl ClassFactory {
    pub fn new() -> Self {
        OBJECT_COUNT.fetch_add(1, Ordering::SeqCst);
        Self
    }
}

impl Drop for ClassFactory {
    fn drop(&mut self) {
        OBJECT_COUNT.fetch_sub(1, Ordering::SeqCst);
    }
}

impl IClassFactory_Impl for ClassFactory_Impl {
    fn CreateInstance(
        &self,
        punkouter: Option<&windows::core::IUnknown>,
        riid: *const GUID,
        ppvobject: *mut *mut c_void,
    ) -> windows::core::Result<()> {
        if ppvobject.is_null() || riid.is_null() {
            return Err(windows::core::Error::from_hresult(E_POINTER));
        }

        unsafe {
            *ppvobject = std::ptr::null_mut();
        }

        if punkouter.is_some() {
            return Err(windows::core::Error::from_hresult(CLASS_E_NOAGGREGATION));
        }

        let provider: windows::Win32::UI::Shell::IThumbnailProvider = ThumbnailProvider::new().into();
        let hr = unsafe { provider.query(riid, ppvobject) };

        if hr.is_err() {
            return Err(windows::core::Error::from_hresult(E_NOINTERFACE));
        }

        Ok(())
    }

    fn LockServer(&self, flock: BOOL) -> windows::core::Result<()> {
        if flock.as_bool() {
            OBJECT_COUNT.fetch_add(1, Ordering::SeqCst);
        } else {
            OBJECT_COUNT.fetch_sub(1, Ordering::SeqCst);
        }
        Ok(())
    }
}
