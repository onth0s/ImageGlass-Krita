//! Linear radiometric pass compositing and accumulation.

use std::collections::HashMap;
use super::channel_resolver::ResolvedPlan;
use super::tone_mapper::{sanitize_alpha, sanitize_radiance};

#[derive(Debug, Clone)]
pub struct LinearRgbaBuffer {
    pub width: usize,
    pub height: usize,
    pub r: Vec<f32>,
    pub g: Vec<f32>,
    pub b: Vec<f32>,
    pub a: Vec<f32>,
}

pub fn composite_linear_passes(
    width: usize,
    height: usize,
    channels: &HashMap<String, Vec<f32>>,
    plan: &ResolvedPlan,
) -> Result<LinearRgbaBuffer, String> {
    let pixel_count = width * height;
    let mut r_buf = vec![0.0f32; pixel_count];
    let mut g_buf = vec![0.0f32; pixel_count];
    let mut b_buf = vec![0.0f32; pixel_count];
    let mut a_buf = vec![1.0f32; pixel_count];

    match plan {
        ResolvedPlan::DirectRgb { r, g, b, a } => {
            let r_src = channels.get(r).ok_or_else(|| format!("Missing channel {}", r))?;
            let g_src = channels.get(g).ok_or_else(|| format!("Missing channel {}", g))?;
            let b_src = channels.get(b).ok_or_else(|| format!("Missing channel {}", b))?;

            for i in 0..pixel_count {
                r_buf[i] = sanitize_radiance(r_src[i]);
                g_buf[i] = sanitize_radiance(g_src[i]);
                b_buf[i] = sanitize_radiance(b_src[i]);
            }

            if let Some(a_name) = a {
                if let Some(a_src) = channels.get(a_name) {
                    for i in 0..pixel_count {
                        a_buf[i] = sanitize_alpha(a_src[i]);
                    }
                }
            }
        }
        ResolvedPlan::DirectLuminance { y, a } => {
            let y_src = channels.get(y).ok_or_else(|| format!("Missing channel {}", y))?;

            for i in 0..pixel_count {
                let val = sanitize_radiance(y_src[i]);
                r_buf[i] = val;
                g_buf[i] = val;
                b_buf[i] = val;
            }

            if let Some(a_name) = a {
                if let Some(a_src) = channels.get(a_name) {
                    for i in 0..pixel_count {
                        a_buf[i] = sanitize_alpha(a_src[i]);
                    }
                }
            }
        }
        ResolvedPlan::MultiPass { passes } => {
            let mut has_alpha = false;
            let mut max_alpha = vec![0.0f32; pixel_count];

            for pass in passes {
                if let (Some(r_src), Some(g_src), Some(b_src)) = (
                    channels.get(&pass.r),
                    channels.get(&pass.g),
                    channels.get(&pass.b),
                ) {
                    for i in 0..pixel_count {
                        r_buf[i] += sanitize_radiance(r_src[i]);
                        g_buf[i] += sanitize_radiance(g_src[i]);
                        b_buf[i] += sanitize_radiance(b_src[i]);
                    }
                }

                if let Some(a_name) = &pass.a {
                    if let Some(a_src) = channels.get(a_name) {
                        has_alpha = true;
                        for i in 0..pixel_count {
                            let a_val = sanitize_alpha(a_src[i]);
                            if a_val > max_alpha[i] {
                                max_alpha[i] = a_val;
                            }
                        }
                    }
                }
            }

            if has_alpha {
                a_buf = max_alpha;
            }
        }
    }

    Ok(LinearRgbaBuffer {
        width,
        height,
        r: r_buf,
        g: g_buf,
        b: b_buf,
        a: a_buf,
    })
}
