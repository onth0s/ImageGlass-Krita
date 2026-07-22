# Native Krita (.kra) Support in ImageGlass v10

This repository contains native, high-performance **Krita (`.kra`)** image format viewing support for [ImageGlass v10](https://github.com/d2phap/imageglass).

---

## 🎨 Overview & Feasibility

Krita `.kra` files are OpenRaster-derived **ZIP archives**. At the root of every `.kra` container, Krita saves flattened preview images:
- **`mergedimage.png`**: The full-resolution, high-quality rendered composite of the entire canvas.
- **`preview.png`**: A smaller thumbnail preview (typically 256×256 px).

Instead of parsing complex proprietary raster layer data or blend modes, ImageGlass extracts `mergedimage.png` directly into memory via `System.IO.Compression.ZipArchive` and feeds it straight to the SkiaSharp rendering engine.

---

## 🛠️ Architecture & Implementation

### 1. `KritaCodec.cs`
- **Location**: [`ImageGlass/source/ImageGlass.Lib/Common/Photoing/Codecs/KraCodecs/KritaCodec.cs`](file:///c:/Users/Leonardo/001/00__DEV/ImageGlass-Krita/ImageGlass/source/ImageGlass.Lib/Common/Photoing/Codecs/KraCodecs/KritaCodec.cs)
- Opens the `.kra` archive and checks for `mergedimage.png` first (full resolution), falling back to `preview.png` if missing.
- Copies entry bytes directly into an in-memory `MemoryStream` so the archive handle closes immediately—**preventing Windows file locks** while working on `.kra` files in Krita.

### 2. `KritaCodecAdapter.cs`
- **Location**: [`ImageGlass/source/ImageGlass.Lib/Common/Photoing/Codecs/KraCodecs/KritaCodecAdapter.cs`](file:///c:/Users/Leonardo/001/00__DEV/ImageGlass-Krita/ImageGlass/source/ImageGlass.Lib/Common/Photoing/Codecs/KraCodecs/KritaCodecAdapter.cs)
- Implements `ICodec` and inherits `PhDisposable`.
- Registered with `CodecId = "krita.skia"`, `MetadataPriority = 200`, and `DecodePriority = 200` to outrank generic fallback decoders.

### 3. Core Registry & Format Integration
- **`CodecRegistry.cs`**: Registered `KritaCodecAdapter` in the constructor and added it to `GetCodecInfos()` as a built-in codec.
- **`Const.cs`**: Appended `.kra` to `Const.IMAGE_FORMATS` for automatic default file associations and settings UI display.

---

## ✅ Test Results & Verification

An automated test suite ([`KraCodecTests`](file:///c:/Users/Leonardo/001/00__DEV/ImageGlass-Krita/ImageGlass/source/KraCodecTests/Program.cs)) was executed against test `.kra` files in `KRA/`:

```text
=== ImageGlass Krita (.kra) Codec Test Suite ===
Found 3 test files in c:\Users\Leonardo\001\00__DEV\ImageGlass-Krita\KRA:

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

---

## 🚀 How to Build & Use

### Prerequisites
- **.NET 10 SDK** (or .NET 8+)
- Visual Studio 2022 / CLI (`dotnet`)

### 1. Build Standalone Executable
From the repository root or `ImageGlass/source` directory:

```bash
cd ImageGlass/source
dotnet publish ImageGlass.Win32/ImageGlass.Win32.csproj -c Release -p:Platform=x64
```

### 2. Location of Published Binary
The standalone compiled executable will be at:
📁 **[`ImageGlass/source/ImageGlass.Win32/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/publish/ImageGlass.exe`](file:///c:/Users/Leonardo/001/00__DEV/ImageGlass-Krita/ImageGlass/source/ImageGlass.Win32/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/publish/ImageGlass.exe)**

### 3. File Association in Windows
To open `.kra` files automatically with this build:
1. Right-click any `.kra` file in Windows Explorer -> **Open with** -> **Choose another app**.
2. Browse to `ImageGlass.exe` in the `publish` folder above.
3. Select **Always use this app to open .kra files**.
