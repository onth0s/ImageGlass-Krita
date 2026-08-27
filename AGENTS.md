# AGENTS.md

## Build

Always build Release with platform specified:

```
dotnet build ImageGlass.Win32 -p:Platform=x64 -c Release
```

## Adding a New Toolbar Icon

When adding a custom icon for a new function, the SVG must be placed in **4 locations** to be available in both the build output and the installer:

### 1. Theme SVGs (build source)

These are the files that get copied to the build output via `<Content Include="..\__assets\__app\**\*.*">` in `ImageGlass.Win32.csproj:89`.

```
ImageGlass/source/__assets/__app/_themes/Kobe/SharedZoom.svg       ← dark theme
ImageGlass/source/__assets/__app/_themes/Kobe-Light/SharedZoom.svg ← light theme
```

### 2. Theme config (`igtheme.json`)

Add the icon mapping to the `ToolbarIcons` dictionary in both themes' `igtheme.json`:

```json
"ToolbarIcons": {
    ...
    "SharedZoom": "SharedZoom.svg"
}
```

### 3. Installer assets (v9 setup)

These are for the installer packaging, not the build:

```
ImageGlass/v9/_Setup/Assets/Themes/Kobe/SharedZoom.svg
ImageGlass/v9/_Setup/Assets/Themes/Kobe-Light/SharedZoom.svg
```

Update the `igtheme.json` in both folders too.

### 4. Code

- **`Config_Static.cs`**: Set `Image = nameof(IgThemeIcon.YourIcon)` (not a hardcoded path).
- **`IgThemeIcons.cs`**: Add the enum value if it's new.
- **`ToolbarItemModel.cs`**: No changes needed — `ImagePath` resolves via `Core.Theme.GetIconPath()`.

## Avalonia in Console Test Runners (Deadlock Gotcha)

When initializing Avalonia in headless/console test runners using:

```csharp
AppBuilder.Configure<Application>()
    .UsePlatformDetect()
    .SetupWithoutStarting();
```

Avalonia installs an `AvaloniaSynchronizationContext` on the thread. In a console application with no active UI message loop (`Dispatcher.UIThread`), any `await` awaiting a task will attempt to post its continuation to the `AvaloniaSynchronizationContext` and **hang/deadlock indefinitely**.

**Fix**: Always reset the synchronization context immediately after `SetupWithoutStarting()`:

```csharp
AppBuilder.Configure<Application>()
    .UsePlatformDetect()
    .SetupWithoutStarting();

SynchronizationContext.SetSynchronizationContext(null);
```

