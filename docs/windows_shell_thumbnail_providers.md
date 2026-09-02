# Windows Explorer Shell Thumbnail Handlers: Architecture & Registry Pitfalls

This document details how Windows Explorer resolves, loads, and executes `IThumbnailProvider` shell extensions, along with critical registry pitfalls to avoid when implementing custom format handlers.

---

## 1. Overview of `IThumbnailProvider` Architecture

When Windows Explorer displays files in folder views with icons or thumbnails, it resolves an `IThumbnailProvider` COM server associated with the file extension.

```
Windows Explorer (`explorer.exe` or `dllhost.exe`)
       │
       ▼
Registry Lookup (ProgID -> Extension -> SystemFileAssociations)
       │
       ▼
Instantiate In-Process COM Server (`CLSID\{GUID}\InprocServer32`)
       │
       ▼
`IInitializeWithStream::Initialize(stream)`
       │
       ▼
`IThumbnailProvider::GetThumbnail(cx, &hbitmap, &alphatype)`
       │
       ▼
Explorer caches output into `%LocalAppData%\Microsoft\Windows\Explorer\thumbcache_*.db`
```

---

## 2. Registry Association Hierarchy & Precedence

Windows Explorer checks for `IThumbnailProvider` handlers using the following fallback hierarchy:

### Precedence 1: Explicit ProgID Association (Highest)
If a file type is associated with a specific application's ProgID (e.g., `.exr` pointing to `OpenEXR Image` or `Photoshop.OpenEXRFile.190`):
```reg
[HKEY_CLASSES_ROOT\<ProgID>\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}]
@="{YOUR-COM-CLSID}"
```

### Precedence 2: Extension Direct Association
Direct registration on the file extension:
```reg
[HKEY_CLASSES_ROOT\.exr\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}]
@="{YOUR-COM-CLSID}"
```

### Precedence 3: Extension-Specific SystemFileAssociations
Extension associations under `SystemFileAssociations`:
```reg
[HKEY_CLASSES_ROOT\SystemFileAssociations\.exr\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}]
@="{YOUR-COM-CLSID}"
```

### Precedence 4: PerceivedType / Generic SystemFileAssociations (Global Fallback)
Windows groups extensions by perceived type (e.g., `PerceivedType = "image"`).
```reg
[HKEY_CLASSES_ROOT\SystemFileAssociations\image\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}]
@="{C7657C4A-9F68-40fa-A4DF-96BC08EB3551}" ; Default Windows Photo Thumbnail Provider
```

---

## 3. ⚠️ Critical Pitfall: Never Override `SystemFileAssociations\image`

> [!CAUTION]
> **Do NOT register a custom format handler under `SystemFileAssociations\image`.**

### What Happens If You Do:
* `SystemFileAssociations\image` applies to **ALL image formats in Windows** (`.png`, `.jpg`, `.jpeg`, `.bmp`, `.gif`, `.webp`, `.tif`, etc.).
* If your custom handler (e.g., an OpenEXR decoder) is registered at `SystemFileAssociations\image`, Windows Explorer will pass **every PNG and JPEG** to your decoder.
* Because standard PNGs and JPEGs fail OpenEXR header validation, your handler returns an error (`E_FAIL`), causing Windows Explorer to fail thumbnail extraction for **all regular images on the system**.
* Regular files will lose their thumbnail previews and display the generic application file icon instead.

### The Correct Approach:
Always scope your thumbnail provider strictly to the specific extension:
```reg
; Correct: scoped only to .exr
[HKEY_CURRENT_USER\Software\Classes\.exr\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}]
@="{B92C3D5E-7840-4A1E-8B39-44F4C1B1E019}"
```

---

## 4. COM Server Registration Requirements

For 64-bit Windows Explorer to load your handler:

1. **InprocServer32 Entry**:
   ```reg
   [HKEY_CURRENT_USER\Software\Classes\CLSID\{B92C3D5E-7840-4A1E-8B39-44F4C1B1E019}]
   @="ImageGlass OpenEXR Thumbnail Provider"

   [HKEY_CURRENT_USER\Software\Classes\CLSID\{B92C3D5E-7840-4A1E-8B39-44F4C1B1E019}\InprocServer32]
   @="C:\\Path\\To\\exr_thumbnail_provider.dll"
   "ThreadingModel"="Apartment"
   ```

2. **64-bit Architecture Match**:
   * 64-bit `explorer.exe` requires a 64-bit (`x86_64-pc-windows-msvc`) DLL. 32-bit DLLs will be ignored by 64-bit Explorer unless running via a 32-bit surrogate.

3. **Alpha Interpretation (`WTS_ALPHATYPE`)**:
   * Return `WTSAT_ARGB` when delivering 32-bit BGRA premultiplied bitmaps (`CreateDIBSection`) to ensure transparent backgrounds blend properly against dark and light Explorer themes.

---

## 5. Troubleshooting & Thumbnail Cache Reset

If thumbnails appear broken or display stale/cached icons:

1. **Broadcast Shell Association Change**:
   ```rust
   unsafe {
       SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, None, None);
   }
   ```

2. **Purge Windows Explorer Thumbnail Cache Database**:
   ```powershell
   taskkill /f /im explorer.exe
   Remove-Item "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\thumbcache_*.db" -Force
   Start-Process explorer.exe
   ```
