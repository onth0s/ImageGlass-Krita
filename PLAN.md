# Plan: Add Krita (.kra) Support to ImageGlass v10

## Summary

Add a new codec adapter to ImageGlass v10 that enables viewing `.kra` files by
extracting the embedded `preview.png` from the ZIP archive and delegating rendering
to the existing SkiaSharp pipeline.

Krita `.kra` files are ZIP archives. At the root of the uncompressed archive is
`preview.png` — a flattened composited preview of the image.

## Files to Create

### `KraCodecs/KritaCodecAdapter.cs` (~100 lines)

Implements `ICodec` (via `PhDisposable`). Registers `.kra` with high priority
to intercept before Skia/Magick.

```
CodecId:          "krita.kra"
CodecName:        "Krita (.kra)"
MetadataPriority: 200      (same as SVG, outranks Skia's 10)
DecodePriority:   200
Extensions:       [".kra"]
```

Methods:
- `CanLoadMetadata(filePath)` -- extension check + `ZipFile.OpenRead` probe
- `LoadMetadataAsync(filePath)` -- extract `preview.png`, read dimensions via
  `SKCodec`, populate `PhotoMetadata`
- `CanDecode(metadata, context)` -- true if extension is `.kra`
- `DecodeAsync(metadata, options, context)` -- extract `preview.png` to
  `MemoryStream`, decode via `SKCodec.Create(stream)` -> `SKImage`, return
  `CodecDecodeResult`

### `KraCodecs/KritaCodec.cs` (~80 lines)

Static helper class for KRA ZIP operations.

Methods:
- `OpenPreviewStream(string filePath)` -> `Stream?`
  Opens the ZIP, finds `preview.png` at root, returns a `MemoryStream` copy
  (caller disposes). Returns null if not found.
- `ReadPreviewMetadata(string filePath)` -> `(uint width, uint height)?`
  Lightweight probe: open ZIP -> extract `preview.png` -> `SKCodec.Create` on
  stream -> read `Info.Width`/`Info.Height` -> dispose.

## File to Modify

### `CodecRegistry.cs` -- 1 line

In the constructor, after existing registrations:

```csharp
Register(new KritaCodecAdapter());
```

## Dependencies

None new. Uses:
- `System.IO.Compression.ZipFile` -- built into .NET 10
- `SkiaSharp.SKCodec` -- already in the project
- Existing `CodecDecodeResult`, `PhotoMetadata`, `PhDisposable` types

## Design Notes

- The `preview.png` is read into a `MemoryStream` so the ZIP file handle is
  released immediately -- no file locking.
- If `preview.png` is missing (corrupted/non-standard .kra), the codec returns
  false from `CanLoadMetadata` and falls through to Magick as a generic ZIP
  reader (which will also fail, surfacing an error to the user).
- No layer-aware compositing -- this is a preview-only viewer, not an editor.
  The `preview.png` is what Krita itself generates for thumbnails.
- Thumbnail generation for gallery view works automatically: `SkiaCodec.LoadThumbnail`
  handles the SKImage once decoded; the KRA adapter only needs to supply the image.

## Estimated Complexity

~180 lines total across 2 new files + 1 line edit. Follows the exact same pattern
as `SvgCodecAdapter` / `SvgCodec`.
