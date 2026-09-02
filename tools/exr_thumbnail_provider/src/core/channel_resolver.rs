//! Channel taxonomy, normalization, and deterministic selection precedence for OpenEXR.

use std::collections::{HashMap, HashSet};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum Component {
    R,
    G,
    B,
    A,
    Y,
}

impl Component {
    pub fn from_str(s: &str) -> Option<Self> {
        match s.to_ascii_uppercase().as_str() {
            "R" | "RED" => Some(Component::R),
            "G" | "GREEN" => Some(Component::G),
            "B" | "BLUE" => Some(Component::B),
            "A" | "ALPHA" => Some(Component::A),
            "Y" | "LUMINANCE" | "GRAY" | "GREY" => Some(Component::Y),
            _ => None,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ParsedChannel {
    pub full_name: String,
    pub layer: String,
    pub component: Component,
}

impl ParsedChannel {
    pub fn parse(full_name: &str) -> Option<Self> {
        let full_name_trimmed = full_name.trim();
        if full_name_trimmed.is_empty() {
            return None;
        }

        if let Some((layer, comp_str)) = full_name_trimmed.rsplit_once('.') {
            let component = Component::from_str(comp_str)?;
            Some(ParsedChannel {
                full_name: full_name_trimmed.to_string(),
                layer: layer.to_string(),
                component,
            })
        } else {
            let component = Component::from_str(full_name_trimmed)?;
            Some(ParsedChannel {
                full_name: full_name_trimmed.to_string(),
                layer: String::new(),
                component,
            })
        }
    }
}

#[derive(Debug, Clone)]
pub struct RgbPass {
    pub layer_name: String,
    pub r: String,
    pub g: String,
    pub b: String,
    pub a: Option<String>,
}

#[derive(Debug, Clone)]
pub enum ResolvedPlan {
    DirectRgb {
        r: String,
        g: String,
        b: String,
        a: Option<String>,
    },
    DirectLuminance {
        y: String,
        a: Option<String>,
    },
    MultiPass {
        passes: Vec<RgbPass>,
    },
}

pub fn resolve_channels(channel_names: &[&str]) -> Option<ResolvedPlan> {
    let parsed: Vec<ParsedChannel> = channel_names
        .iter()
        .filter_map(|name| ParsedChannel::parse(name))
        .collect();

    // Group by layer
    let mut layer_map: HashMap<String, HashMap<Component, String>> = HashMap::new();
    for ch in parsed {
        layer_map
            .entry(ch.layer)
            .or_default()
            .insert(ch.component, ch.full_name);
    }

    // Helper to extract an RGB(A) pass from a layer map entry
    let get_rgb_pass = |layer: &str, comps: &HashMap<Component, String>| -> Option<RgbPass> {
        let r = comps.get(&Component::R)?.clone();
        let g = comps.get(&Component::G)?.clone();
        let b = comps.get(&Component::B)?.clone();
        let a = comps.get(&Component::A).cloned();
        Some(RgbPass {
            layer_name: layer.to_string(),
            r,
            g,
            b,
            a,
        })
    };

    // Precedence 1: Explicit Composite / Combined layers
    const COMBINED_NAMES: &[&str] = &[
        "combined",
        "composite",
        "rgba",
        "beauty",
        "viewlayer.combined",
        "viewlayer.composite",
    ];
    for (layer, comps) in &layer_map {
        let layer_lower = layer.to_ascii_lowercase();
        if COMBINED_NAMES.contains(&layer_lower.as_str()) {
            if let Some(pass) = get_rgb_pass(layer, comps) {
                return Some(ResolvedPlan::DirectRgb {
                    r: pass.r,
                    g: pass.g,
                    b: pass.b,
                    a: pass.a,
                });
            }
        }
    }

    // Precedence 2: Root RGB(A) or Root Y
    if let Some(root_comps) = layer_map.get("") {
        if let Some(pass) = get_rgb_pass("", root_comps) {
            return Some(ResolvedPlan::DirectRgb {
                r: pass.r,
                g: pass.g,
                b: pass.b,
                a: pass.a,
            });
        }
        if let Some(y) = root_comps.get(&Component::Y) {
            let a = root_comps.get(&Component::A).cloned();
            return Some(ResolvedPlan::DirectLuminance {
                y: y.clone(),
                a,
            });
        }
    }

    // Precedence 3: Known Blender Multi-pass Heuristic (diffuse + specular)
    let has_diffuse = layer_map.keys().any(|k| k.to_ascii_lowercase().ends_with("diffuse") || k.to_ascii_lowercase() == "diffuse");
    let has_specular = layer_map.keys().any(|k| k.to_ascii_lowercase().ends_with("specular") || k.to_ascii_lowercase() == "specular");

    if has_diffuse && has_specular {
        let ignored_passes: HashSet<&str> = [
            "normal", "depth", "z", "position", "pos", "vector", "uv", "crypto",
            "cryptomatte", "index", "albedo", "shadow", "ao", "ambientocclusion"
        ].into_iter().collect();

        let mut passes = Vec::new();
        for (layer, comps) in &layer_map {
            let layer_lower = layer.to_ascii_lowercase();
            let suffix = layer_lower.rsplit('.').next().unwrap_or(&layer_lower);
            if ignored_passes.contains(suffix) {
                continue;
            }
            if let Some(pass) = get_rgb_pass(layer, comps) {
                passes.push(pass);
            }
        }

        if !passes.is_empty() {
            return Some(ResolvedPlan::MultiPass { passes });
        }
    }

    // Precedence 4: First valid RGB(A) layer (alphabetical order for determinism)
    let mut sorted_layers: Vec<_> = layer_map.keys().collect();
    sorted_layers.sort();

    for layer in sorted_layers {
        if let Some(comps) = layer_map.get(layer) {
            if let Some(pass) = get_rgb_pass(layer, comps) {
                return Some(ResolvedPlan::DirectRgb {
                    r: pass.r,
                    g: pass.g,
                    b: pass.b,
                    a: pass.a,
                });
            }
        }
    }

    None
}
