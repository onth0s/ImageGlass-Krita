//! Windows Explorer OpenEXR Thumbnail Provider Shell Extension.

pub mod com;
pub mod core;

use std::ffi::c_void;
use std::sync::atomic::{AtomicIsize, Ordering};
use windows::core::{GUID, HRESULT, Interface};
use windows::Win32::Foundation::{
    BOOL, CLASS_E_CLASSNOTAVAILABLE, E_FAIL, E_POINTER, HMODULE,
    HINSTANCE, S_FALSE, S_OK,
};
use windows::Win32::System::Com::IClassFactory;
use windows::Win32::System::SystemServices::{DLL_PROCESS_ATTACH, DLL_PROCESS_DETACH};

use com::{ClassFactory, OBJECT_COUNT, CLSID_EXR_THUMBNAIL_PROVIDER};

static MODULE_HANDLE: AtomicIsize = AtomicIsize::new(0);

#[no_mangle]
#[allow(non_snake_case)]
pub extern "system" fn DllMain(
    hinst_dll: HINSTANCE,
    fdw_reason: u32,
    _lpv_reserved: *mut c_void,
) -> BOOL {
    match fdw_reason {
        DLL_PROCESS_ATTACH => {
            MODULE_HANDLE.store(hinst_dll.0 as isize, Ordering::SeqCst);
        }
        DLL_PROCESS_DETACH => {
            MODULE_HANDLE.store(0, Ordering::SeqCst);
        }
        _ => {}
    }
    BOOL(1)
}

#[no_mangle]
#[allow(non_snake_case)]
pub extern "system" fn DllGetClassObject(
    rclsid: *const GUID,
    riid: *const GUID,
    ppv: *mut *mut c_void,
) -> HRESULT {
    let result = std::panic::catch_unwind(|| {
        if rclsid.is_null() || riid.is_null() || ppv.is_null() {
            return E_POINTER;
        }

        unsafe {
            *ppv = std::ptr::null_mut();
            if *rclsid != CLSID_EXR_THUMBNAIL_PROVIDER {
                return CLASS_E_CLASSNOTAVAILABLE;
            }
        }

        let factory: IClassFactory = ClassFactory::new().into();
        unsafe { factory.query(riid, ppv) }
    });

    result.unwrap_or(E_FAIL)
}

#[no_mangle]
#[allow(non_snake_case)]
pub extern "system" fn DllCanUnloadNow() -> HRESULT {
    let result = std::panic::catch_unwind(|| {
        if OBJECT_COUNT.load(Ordering::SeqCst) == 0 {
            S_OK
        } else {
            S_FALSE
        }
    });

    result.unwrap_or(S_FALSE)
}

#[no_mangle]
#[allow(non_snake_case)]
pub extern "system" fn DllRegisterServer() -> HRESULT {
    let result = std::panic::catch_unwind(|| {
        let hmodule = HMODULE(MODULE_HANDLE.load(Ordering::SeqCst) as *mut _);
        match com::register_server(hmodule) {
            Ok(()) => S_OK,
            Err(e) => e.code(),
        }
    });

    result.unwrap_or(E_FAIL)
}

#[no_mangle]
#[allow(non_snake_case)]
pub extern "system" fn DllUnregisterServer() -> HRESULT {
    let result = std::panic::catch_unwind(|| match com::unregister_server() {
        Ok(()) => S_OK,
        Err(e) => e.code(),
    });

    result.unwrap_or(E_FAIL)
}
