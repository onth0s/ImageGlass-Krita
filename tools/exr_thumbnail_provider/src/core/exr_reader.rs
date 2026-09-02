//! OpenEXR stream decoding with strict resource limits.

use std::collections::HashMap;
use std::io::{Read, Seek};
use exr::prelude::*;
use thiserror::Error;

use super::channel_resolver::resolve_channels;
use super::compositor::composite_linear_passes;
use super::limits::{MAX_DECODED_BYTES, MAX_DIMENSION, MAX_PIXELS};
use super::resizer::{process_and_resize, ThumbnailOutput};

#[derive(Error, Debug)]
pub enum CoreError {
    #[error("I/O error: {0}")]
    Io(#[from] std::io::Error),

    #[error("EXR decode error: {0}")]
    Exr(String),

    #[error("Resource limit exceeded: {0}")]
    ResourceLimit(String),

    #[error("No usable RGB/RGBA channels found in image")]
    NoUsableChannels,

    #[error("Processing error: {0}")]
    Processing(String),
}

pub fn decode_and_generate_thumbnail<R: Read + Seek>(
    mut reader: R,
    cx: u32,
    exposure: f32,
) -> std::result::Result<ThumbnailOutput, CoreError> {
    // 1. Inspect headers without loading pixel data
    let meta = MetaData::read_from_unbuffered(&mut reader, false)
        .map_err(|e| CoreError::Exr(e.to_string()))?;

    // Check all headers / parts against dimension and allocation limits
    for (i, header) in meta.headers.iter().enumerate() {
        let size = header.layer_size;
        let w = size.width();
        let h = size.height();

        if w > MAX_DIMENSION || h > MAX_DIMENSION {
            return Err(CoreError::ResourceLimit(format!(
                "Header {} dimensions {}x{} exceed max limit {}",
                i, w, h, MAX_DIMENSION
            )));
        }

        let pixel_count = w.saturating_mul(h);
        if pixel_count > MAX_PIXELS {
            return Err(CoreError::ResourceLimit(format!(
                "Header {} pixel count {} exceeds max limit {}",
                i, pixel_count, MAX_PIXELS
            )));
        }

        let channel_count = header.channels.list.len();
        let estimated_bytes = pixel_count
            .saturating_mul(channel_count)
            .saturating_mul(4); // 4 bytes per f32 sample

        if estimated_bytes > MAX_DECODED_BYTES {
            return Err(CoreError::ResourceLimit(format!(
                "Header {} estimated allocation {} MB exceeds max budget {} MB",
                i,
                estimated_bytes / (1024 * 1024),
                MAX_DECODED_BYTES / (1024 * 1024)
            )));
        }
    }

    // Seek back to start before reading pixel data
    reader
        .seek(std::io::SeekFrom::Start(0))
        .map_err(CoreError::Io)?;

    // 2. Read flat layers from the EXR stream using largest resolution level
    let image = read()
        .no_deep_data()
        .largest_resolution_level()
        .all_channels()
        .all_layers()
        .all_attributes()
        .from_unbuffered(reader)
        .map_err(|e| CoreError::Exr(e.to_string()))?;

    // We take the first part/layer
    let layer = image
        .layer_data
        .into_iter()
        .next()
        .ok_or_else(|| CoreError::Exr("No layers in EXR file".to_string()))?;

    let width = layer.size.width();
    let height = layer.size.height();

    // 3. Extract channel names and sample vectors
    let mut channel_map: HashMap<String, Vec<f32>> = HashMap::new();
    for ch in layer.channel_data.list {
        let name = ch.name.to_string();
        let samples: Vec<f32> = match ch.sample_data {
            FlatSamples::F32(vec) => vec,
            FlatSamples::F16(vec) => vec.into_iter().map(|h| h.to_f32()).collect(),
            FlatSamples::U32(vec) => vec.into_iter().map(|u| u as f32 / u32::MAX as f32).collect(),
        };
        channel_map.insert(name, samples);
    }

    let channel_names: Vec<&str> = channel_map.keys().map(|s| s.as_str()).collect();

    // 4. Resolve pass selection precedence
    let plan = resolve_channels(&channel_names).ok_or(CoreError::NoUsableChannels)?;

    // 5. Composite linear passes
    let linear_buffer = composite_linear_passes(width, height, &channel_map, &plan)
        .map_err(CoreError::Processing)?;

    // 6. Tone map & downsample to target thumbnail size
    let thumbnail = process_and_resize(&linear_buffer, cx, exposure)
        .map_err(CoreError::Processing)?;

    Ok(thumbnail)
}
