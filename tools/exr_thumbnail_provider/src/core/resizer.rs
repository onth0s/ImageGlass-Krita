//! Aspect-preserving image resizing and BGRA premultiplied output formatting.

use fast_image_resize::images::Image;
use fast_image_resize::{FilterType, PixelType, ResizeAlg, ResizeOptions, Resizer};

use super::compositor::LinearRgbaBuffer;
use super::tone_mapper::map_radiance_to_srgb;

#[derive(Debug, Clone)]
pub struct ThumbnailOutput {
    pub width: u32,
    pub height: u32,
    pub bgra_premultiplied: Vec<u8>,
}

/// Calculates integer aspect-preserving target dimensions bounded within cx x cx.
pub fn calculate_target_dimensions(src_w: u32, src_h: u32, cx: u32) -> (u32, u32) {
    if src_w == 0 || src_h == 0 || cx == 0 {
        return (1, 1);
    }

    let scale_w = (cx as f64) / (src_w as f64);
    let scale_h = (cx as f64) / (src_h as f64);
    let scale = scale_w.min(scale_h).min(1.0); // Never upscale

    let dst_w = ((src_w as f64 * scale).round() as u32).clamp(1, cx);
    let dst_h = ((src_h as f64 * scale).round() as u32).clamp(1, cx);

    (dst_w, dst_h)
}

pub fn process_and_resize(
    buffer: &LinearRgbaBuffer,
    cx: u32,
    exposure: f32,
) -> Result<ThumbnailOutput, String> {
    let src_w = buffer.width as u32;
    let src_h = buffer.height as u32;

    let (dst_w, dst_h) = calculate_target_dimensions(src_w, src_h, cx);

    // 1. Tone map & encode linear HDR to sRGB RGBA8888 (unpremultiplied for resizing)
    let pixel_count = (src_w * src_h) as usize;
    let mut srgb_rgba = vec![0u8; pixel_count * 4];

    for i in 0..pixel_count {
        let r_srgb = map_radiance_to_srgb(buffer.r[i], exposure);
        let g_srgb = map_radiance_to_srgb(buffer.g[i], exposure);
        let b_srgb = map_radiance_to_srgb(buffer.b[i], exposure);
        let a = buffer.a[i].clamp(0.0, 1.0);

        srgb_rgba[i * 4] = (r_srgb * 255.0 + 0.5) as u8;
        srgb_rgba[i * 4 + 1] = (g_srgb * 255.0 + 0.5) as u8;
        srgb_rgba[i * 4 + 2] = (b_srgb * 255.0 + 0.5) as u8;
        srgb_rgba[i * 4 + 3] = (a * 255.0 + 0.5) as u8;
    }

    // 2. Downsample if target size differs
    let (final_w, final_h, final_rgba) = if dst_w != src_w || dst_h != src_h {
        let src_image = Image::from_vec_u8(src_w, src_h, srgb_rgba, PixelType::U8x4)
            .map_err(|e| e.to_string())?;

        let mut dst_image = Image::new(dst_w, dst_h, PixelType::U8x4);

        let mut resizer = Resizer::new();
        let options = ResizeOptions::new().resize_alg(ResizeAlg::Convolution(FilterType::Lanczos3));
        resizer
            .resize(&src_image, &mut dst_image, &options)
            .map_err(|e| e.to_string())?;

        (dst_w, dst_h, dst_image.into_vec())
    } else {
        (src_w, src_h, srgb_rgba)
    };

    // 3. Convert RGBA to Premultiplied BGRA8888 for Windows GDI DIBSection
    let out_pixel_count = (final_w * final_h) as usize;
    let mut bgra_premul = vec![0u8; out_pixel_count * 4];

    for i in 0..out_pixel_count {
        let r = final_rgba[i * 4] as f32;
        let g = final_rgba[i * 4 + 1] as f32;
        let b = final_rgba[i * 4 + 2] as f32;
        let a = (final_rgba[i * 4 + 3] as f32) / 255.0;

        let b_premul = (b * a + 0.5).clamp(0.0, 255.0) as u8;
        let g_premul = (g * a + 0.5).clamp(0.0, 255.0) as u8;
        let r_premul = (r * a + 0.5).clamp(0.0, 255.0) as u8;
        let a_byte = (a * 255.0 + 0.5).clamp(0.0, 255.0) as u8;

        bgra_premul[i * 4] = b_premul;
        bgra_premul[i * 4 + 1] = g_premul;
        bgra_premul[i * 4 + 2] = r_premul;
        bgra_premul[i * 4 + 3] = a_byte;
    }

    Ok(ThumbnailOutput {
        width: final_w,
        height: final_h,
        bgra_premultiplied: bgra_premul,
    })
}
