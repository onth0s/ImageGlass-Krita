use std::fs::File;
use std::path::PathBuf;
use exr_thumbnail_provider::core::decode_and_generate_thumbnail;

fn get_scratch_dir() -> PathBuf {
    let mut p = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    p.pop(); // tools
    p.pop(); // repo root
    p.push("scratch");
    p
}

#[test]
fn test_decode_toon_light_matcap() {
    let path = get_scratch_dir().join("toon_light.exr");
    assert!(path.exists(), "toon_light.exr must exist in scratch/");

    let file = File::open(&path).expect("failed to open toon_light.exr");
    let thumb = decode_and_generate_thumbnail(file, 256, 0.0).expect("failed to generate thumbnail");

    assert!(thumb.width > 0 && thumb.width <= 256);
    assert!(thumb.height > 0 && thumb.height <= 256);
    assert_eq!(thumb.bgra_premultiplied.len(), (thumb.width * thumb.height * 4) as usize);

    // Center pixel of toon_light matcap should not be black
    let cx = thumb.width / 2;
    let cy = thumb.height / 2;
    let idx = ((cy * thumb.width + cx) * 4) as usize;
    let b = thumb.bgra_premultiplied[idx];
    let g = thumb.bgra_premultiplied[idx + 1];
    let r = thumb.bgra_premultiplied[idx + 2];
    let a = thumb.bgra_premultiplied[idx + 3];

    println!("toon_light.exr center pixel: BGRA({}, {}, {}, {})", b, g, r, a);
    assert!(r > 50, "Red component of toon_light center pixel must be non-zero");
    assert!(g > 50, "Green component of toon_light center pixel must be non-zero");
    assert!(b > 50, "Blue component of toon_light center pixel must be non-zero");
    assert_eq!(a, 255, "Alpha must be 255 for opaque center");
}

#[test]
fn test_decode_check_normal() {
    let path = get_scratch_dir().join("check_normal+y.exr");
    assert!(path.exists(), "check_normal+y.exr must exist in scratch/");

    let file = File::open(&path).expect("failed to open check_normal+y.exr");
    let thumb = decode_and_generate_thumbnail(file, 128, 0.0).expect("failed to generate thumbnail");

    assert_eq!(thumb.width, 128);
    assert_eq!(thumb.height, 128);

    let cx = thumb.width / 2;
    let cy = thumb.height / 2;
    let idx = ((cy * thumb.width + cx) * 4) as usize;
    let b = thumb.bgra_premultiplied[idx];
    let g = thumb.bgra_premultiplied[idx + 1];
    let r = thumb.bgra_premultiplied[idx + 2];

    println!("check_normal+y.exr center pixel: BGRA({}, {}, {})", b, g, r);
    assert!(b > 100, "Blue normal component must be prominent");
}

#[test]
fn test_decode_spec_2() {
    let path = get_scratch_dir().join("spec_2.exr");
    assert!(path.exists(), "spec_2.exr must exist in scratch/");

    let file = File::open(&path).expect("failed to open spec_2.exr");
    let thumb = decode_and_generate_thumbnail(file, 96, 0.0).expect("failed to generate thumbnail");

    assert_eq!(thumb.width, 96);
    assert_eq!(thumb.height, 96);

    let cx = thumb.width / 2;
    let cy = thumb.height / 2;
    let idx = ((cy * thumb.width + cx) * 4) as usize;
    let b = thumb.bgra_premultiplied[idx];
    let g = thumb.bgra_premultiplied[idx + 1];
    let r = thumb.bgra_premultiplied[idx + 2];

    println!("spec_2.exr center pixel: BGRA({}, {}, {})", b, g, r);
    assert!(r > 100 && g > 100 && b > 100);
}
