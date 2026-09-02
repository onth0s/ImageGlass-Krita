fn main() {
    println!("cargo:rustc-cdylib-link-arg=/DEF:exr_thumbnail_provider.def");
}
