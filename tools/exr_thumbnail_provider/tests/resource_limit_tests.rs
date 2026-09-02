use std::io::Cursor;
use exr_thumbnail_provider::core::decode_and_generate_thumbnail;

#[test]
fn test_empty_stream_fails_gracefully() {
    let empty_data = vec![];
    let cursor = Cursor::new(empty_data);
    let result = decode_and_generate_thumbnail(cursor, 128, 0.0);
    assert!(result.is_err(), "Empty stream must return an error");
}

#[test]
fn test_corrupted_stream_fails_gracefully() {
    let corrupt_data = vec![0x76, 0x2f, 0x31, 0x01, 0xFF, 0xFF, 0x00]; // Invalid EXR magic/header
    let cursor = Cursor::new(corrupt_data);
    let result = decode_and_generate_thumbnail(cursor, 128, 0.0);
    assert!(result.is_err(), "Corrupted stream must return an error without panicking");
}
