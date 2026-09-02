//! Pure Rust OpenEXR thumbnail generation core.
//! This module has zero COM, Win32, or GDI dependencies and is 100% testable on any target.

pub mod channel_resolver;
pub mod compositor;
pub mod exr_reader;
pub mod limits;
pub mod resizer;
pub mod tone_mapper;

pub use exr_reader::{decode_and_generate_thumbnail, CoreError};
pub use resizer::ThumbnailOutput;
