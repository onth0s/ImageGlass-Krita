# Native Krita (.kra) Support & Shared Zoom in ImageGlass v10

This repository contains custom high-performance extensions for [ImageGlass v10](https://github.com/d2phap/imageglass), featuring native **Krita (`.kra`)** image format viewing support and an upgraded **Shared Zoom & Pan** navigation engine.

---

## 🚀 Key Features & Architectural Enhancements

### 1. 🎨 Native Krita (`.kra`) Codec Support
Krita `.kra` files are OpenRaster-derived ZIP archives. At the root of every `.kra` container, Krita saves flattened preview images:
- **`mergedimage.png`**: The full-resolution, high-quality rendered composite of the canvas.
- **`preview.png`**: A smaller thumbnail preview (typically 256×256 px).

#### Implementation Details:
- **`KritaCodec.cs`** ([`ImageGlass/source/ImageGlass.Lib/Common/Photoing/Codecs/KraCodecs/KritaCodec.cs`](file:///c:/Users/Leonardo/001/00__DEV/ImageGlass-Krita/ImageGlass/source/ImageGlass.Lib/Common/Photoing/Codecs/KraCodecs/KritaCodec.cs)):
  - Extracts `mergedimage.png` directly into an in-memory `MemoryStream`, falling back to `preview.png` if missing.
  - Releases archive handles immediately upon reading—**preventing Windows file locks** while editing `.kra` files in Krita.
  - Decodes images via SkiaSharp (`SKImage.FromEncodedData`).
- **`KritaCodecAdapter.cs`** ([`ImageGlass/source/ImageGlass.Lib/Common/Photoing/Codecs/KraCodecs/KritaCodecAdapter.cs`](file:///c:/Users/Leonardo/001/00__DEV/ImageGlass-Krita/ImageGlass/source/ImageGlass.Lib/Common/Photoing/Codecs/KraCodecs/KritaCodecAdapter.cs)):
  - Registered with `CodecId = "krita.skia"` with elevated `MetadataPriority` and `DecodePriority` (200) to outrank generic decoders.
- **Registry Integration**: Integrated into `CodecRegistry.cs` and `Const.IMAGE_FORMATS` for automatic `.kra` file association.

---

### 2. 🔍 Synchronized Shared Zoom Engine
When comparing multiple rendered assets or artwork iterations, **Shared Zoom** locks the zoom level and view position across image transitions.

#### Core Mechanics:
- **Viewport-Center Fractional Pan Coordinates**:
  - Uses normalized center fractions (`SharedZoomCenterFracX`, `SharedZoomCenterFracY`) relative to image dimensions rather than raw pixel offsets.
  - Maintains subject focus seamlessly across images of different aspect ratios and resolutions without edge clipping or jumping.
- **Preview Race Condition Prevention**:
  - Bypasses preview buffer rendering while Shared Zoom is active to prevent lower-resolution preview frames from resetting viewport coordinates during rapid navigation.
- **UI & Theme Integration**:
  - Integrated into the main toolbar and main menu.
  - Includes custom theme assets (`SharedZoom.svg`) across `Kobe` (dark) and `Kobe-Light` themes.

---

### 3. 🖐️ Configurable Over-Panning Engine
Enables panning past standard image boundaries until only 10% of the closest image edge remains visible in the viewport.
- **Dynamic Over-Pan Clamping**: Computes dynamic margins for both overflowing and fitting (FreePan) axes so up to 90% of the viewport/image dimension can be panned out of view.
- **Seamless Shared Zoom Integration**: Calculates and preserves over-panned normalized center fractions (`_sharedZoomPanNormX/Y`) across photo transitions.
- **UI & Context Menu Integration**: Includes an `EnableOverPan` setting (enabled by default) in **Settings → Viewer → Panning** as well as a quick toggle directly in the right-click menu (**Panning → Enable over-panning**).

---

### 4. 🖼️ Multi-Layer OpenEXR (.exr) & Blender Matcap Compositing
Enhanced OpenEXR decoding to support complex multi-channel and multi-layer EXRs:
- **Dynamic Pass Resolution**: Discovers and maps layer prefixes (`diffuse.*`, `specular.*`, `Combined.*`) via metadata and header channel lists.
- **Beauty Pass Compositing**: Dynamically accumulates `diffuse + specular` passes in linear float space for Blender default matcap rendering.
- **Thumbnail Gallery Pipeline**: Bypasses external Windows Shell thumbnail extraction for HDR/EXR formats in ImageGlass, ensuring internal HDR float decoding and tone-mapped thumbnails.

---

### 5. 🦀 Native Rust Windows Explorer OpenEXR Thumbnail Provider (`tools/exr_thumbnail_provider`)
A high-performance, panic-safe native 64-bit Windows Shell Extension (`IThumbnailProvider` + `IInitializeWithStream`) written in pure Rust:
- **Zero Runtime Dependencies**: Self-contained 64-bit DLL built with official Microsoft `windows` crate and pure Rust `exr` crate.
- **Deterministic Color & Tone Pipeline**: Linear radiometric pass compositing, Reinhard tone mapping, and IEC 61966-2-1 sRGB transfer encoding with alpha premultiplication (`WTSAT_ARGB`).
- **Safety Contract**: Hard input limits (256MB file, 128MB decoded allocation), stream re-entrancy, and complete panic isolation at COM ABI boundaries.
- **Registration**: Includes `register.bat` / `unregister.bat` for instant installation.

---

### 6. 🛠️ Hotkey & Stability Fixes
- **Hotkey Parsing**: Resolved exceptions during hotkey string serialization and custom keybinding initialization (`Hotkey.cs`).
- **Debug Message Action**: Mapped key `S` to trigger on-screen debug diagnostic logging (`AppAPIProvider_Hotkeys.cs`).
- **Preview Buffering Toggle**: Disabled asynchronous image preview buffering (`IsBufferedPreview = false`) for crisp, immediate frame rendering.

---

## ✅ Empirical Verification & Diagnostics

### 1. Krita Codec Test Suite (`KraCodecTests`)
Verified against `.kra` test samples in `KRA/`:

```text
=== ImageGlass Krita (.kra) Codec Test Suite ===
Found 3 test files in KRA/:

--- Testing: foot_and_leg_study.kra ---
  IsKraFile: True
  Preview stream extracted: 4,693,362 bytes
  Metadata: Width=2400, Height=1792, HasAlpha=True
  Decoded SKImage: Width=2400, Height=1792
  CodecRegistry Selected: Krita (.kra) (ID: krita.skia)
  ✅ PASSED

--- Testing: glossy_lips_study.kra ---
  IsKraFile: True
  Preview stream extracted: 329,377 bytes
  Metadata: Width=1831, Height=1496, HasAlpha=True
  Decoded SKImage: Width=1831, Height=1496
  CodecRegistry Selected: Krita (.kra) (ID: krita.skia)
  ✅ PASSED

--- Testing: portrait.kra ---
  IsKraFile: True
  Preview stream extracted: 10,700,249 bytes
  Metadata: Width=2048, Height=2731, HasAlpha=True
  Decoded SKImage: Width=2048, Height=2731
  CodecRegistry Selected: Krita (.kra) (ID: krita.skia)
  ✅ PASSED

=== RESULTS: 3/3 Tests Passed ===
```

### 2. Shared Zoom Homology Suite
- 12/12 test scenarios passing across landscape, portrait, and square images verifying exact pixel color homology and pan ratio stability.

---

## 📦 How to Build & Install

### Option A: Automated Build Script (Recommended)
Run the automated PowerShell build script from the repository root:

```powershell
.\build.ps1
```
*Note: `build.ps1` automatically terminates running ImageGlass instances to release file locks before performing an `x64 Release` build.*

### Option B: Manual CLI Build
From the repository root, execute:

```bash
dotnet build ImageGlass/source/ImageGlass.Win32 -p:Platform=x64 -c Release
```

---

### Deployment to System Program Files

1. Navigate to the build output directory:
   📁 [`ImageGlass/source/ImageGlass.Win32/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/`](file:///c:/Users/Leonardo/001/00__DEV/ImageGlass-Krita/ImageGlass/source/ImageGlass.Win32/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/)

2. Copy all output files and overwrite your existing installation in:
   `C:\Program Files\ImageGlass\`

3. Launch ImageGlass and set file associations for `.kra` files!
