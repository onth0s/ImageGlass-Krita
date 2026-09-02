//! Hard safety limits and guardrails for OpenEXR processing.

/// Maximum allowable input file stream size (256 MB).
pub const MAX_FILE_BYTES: u64 = 256 * 1024 * 1024;

/// Maximum width or height dimension for image headers (8,192 pixels).
pub const MAX_DIMENSION: usize = 8192;

/// Maximum total pixel count (8192 x 8192 = 67,108,864 pixels).
pub const MAX_PIXELS: usize = 8192 * 8192;

/// Maximum uncompressed buffer allocation budget for decoding (128 MB).
pub const MAX_DECODED_BYTES: usize = 128 * 1024 * 1024;

/// Maximum defensive finite saturation bound for +Infinity radiance values.
pub const MAX_SANITIZED_RADIANCE: f32 = 65504.0;
