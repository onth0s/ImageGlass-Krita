//! Radiometric sanitization, deterministic Reinhard tone mapping, and sRGB transfer encoding.

use super::limits::MAX_SANITIZED_RADIANCE;

/// Sanitizes raw float values according to the defensive policy.
#[inline]
pub fn sanitize_radiance(val: f32) -> f32 {
    if val.is_nan() || val <= 0.0 {
        0.0
    } else if val.is_infinite() {
        MAX_SANITIZED_RADIANCE
    } else {
        val.min(MAX_SANITIZED_RADIANCE)
    }
}

/// Sanitizes alpha float values to [0.0, 1.0].
#[inline]
pub fn sanitize_alpha(val: f32) -> f32 {
    if val.is_nan() || val <= 0.0 {
        0.0
    } else if val >= 1.0 {
        1.0
    } else {
        val
    }
}

/// Applies Reinhard tone reproduction: C / (1.0 + C).
#[inline]
pub fn reinhard_tone_map(radiance: f32, exposure: f32) -> f32 {
    let sanitized = sanitize_radiance(radiance);
    let exposed = sanitized * (2.0f32).powf(exposure);
    exposed / (1.0 + exposed)
}

/// IEC 61966-2-1 standard linear-to-sRGB transfer curve.
#[inline]
pub fn linear_to_srgb(linear: f32) -> f32 {
    let clamped = linear.clamp(0.0, 1.0);
    if clamped <= 0.0031308 {
        12.92 * clamped
    } else {
        1.055 * clamped.powf(1.0 / 2.4) - 0.055
    }
}

/// Full tone map pipeline: Radiance f32 -> sRGB [0.0, 1.0].
#[inline]
pub fn map_radiance_to_srgb(radiance: f32, exposure: f32) -> f32 {
    let display_linear = reinhard_tone_map(radiance, exposure);
    linear_to_srgb(display_linear)
}
