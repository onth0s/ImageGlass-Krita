/*
ImageGlass - A Fast, Seamless Photo Viewer
Copyright (C) 2010 - 2026 DUONG DIEU PHAP
Project homepage: https://imageglass.org

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using System;
using System.Diagnostics;
using System.IO;

namespace ImageGlass.Linux.Common;

/// <summary>
/// Thin wrapper over the XDG desktop portals (<c>org.freedesktop.portal.*</c>) and
/// the file-manager D-Bus interface (<c>org.freedesktop.FileManager1</c>), invoked
/// through the <c>gdbus</c> CLI that ships in the runtime.
/// </summary>
/// <remarks>
/// Using <c>gdbus</c> avoids a managed D-Bus dependency. Crucially, <c>gdbus call</c>
/// is SYNCHRONOUS — it waits for the method reply before exiting — so the message is
/// reliably delivered through the Flatpak D-Bus proxy even though the calling app
/// does not wait for the gdbus process. (Fire-and-forget <c>dbus-send</c> without
/// <c>--print-reply</c> proved unreliable from inside the sandbox.) These calls take
/// string/URI arguments only — no file-descriptor passing — and the app is granted
/// <c>--filesystem=host</c> plus <c>--talk-name=org.freedesktop.FileManager1</c>, so
/// the host backend can resolve the real paths. Trashing is handled separately
/// (see <see cref="FreeDesktopTrash"/>) because the Trash portal is unreliable on
/// some desktops; Email/Print portals are not used because they require FD passing.
/// </remarks>
internal static class XdgPortal
{
    private const string PORTAL_DEST = "org.freedesktop.portal.Desktop";
    private const string PORTAL_PATH = "/org/freedesktop/portal/desktop";
    private const string FM_DEST = "org.freedesktop.FileManager1";
    private const string FM_PATH = "/org/freedesktop/FileManager1";


    /// <summary>
    /// Sets the desktop wallpaper to <paramref name="filePath"/> via
    /// <c>org.freedesktop.portal.Wallpaper.SetWallpaperURI</c>. The portal shows
    /// its preview/confirm dialog. Best-effort and non-blocking.
    /// </summary>
    public static void SetWallpaper(string filePath)
    {
        Call(PORTAL_DEST, PORTAL_PATH, "org.freedesktop.portal.Wallpaper.SetWallpaperURI",
            "",     // parent_window
            ToFileUri(filePath),
            "{'show-preview': <true>, 'set-on': <'both'>}");
    }


    /// <summary>
    /// Opens a file or folder in its default handler via the OpenURI portal
    /// (<c>org.freedesktop.portal.OpenURI.OpenURI</c>). The portal raises/focuses
    /// the target window correctly under Wayland. Best-effort and non-blocking.
    /// </summary>
    public static void OpenPath(string path)
    {
        Call(PORTAL_DEST, PORTAL_PATH, "org.freedesktop.portal.OpenURI.OpenURI",
            "",     // parent_window
            ToFileUri(path),
            "{}");  // options
    }


    /// <summary>
    /// Reveals <paramref name="filePath"/> in the default file manager, selecting
    /// it (or showing its properties dialog when <paramref name="showProperties"/>
    /// is <c>true</c>) via the <c>org.freedesktop.FileManager1</c> interface.
    /// Best-effort and non-blocking.
    /// </summary>
    public static void ShowInFileManager(string filePath, bool showProperties = false)
    {
        // FileManager1 takes an array of URIs, written here as GVariant text. Also
        // percent-encode the apostrophe so a filename containing one cannot break
        // out of the single-quoted GVariant string literal.
        var uri = ToFileUri(filePath).Replace("'", "%27");
        var method = showProperties
            ? "org.freedesktop.FileManager1.ShowItemProperties"
            : "org.freedesktop.FileManager1.ShowItems";

        Call(FM_DEST, FM_PATH, method,
            $"['{uri}']",   // URIs
            "");            // startup_id
    }


    /// <summary>
    /// Builds a percent-encoded <c>file://</c> URI for <paramref name="path"/>.
    /// </summary>
    private static string ToFileUri(string path)
    {
        return new Uri(Path.GetFullPath(path)).AbsoluteUri;
    }


    /// <summary>
    /// Invokes a D-Bus method via <c>gdbus call</c>. Arguments are passed through
    /// <see cref="ProcessStartInfo.ArgumentList"/> so no manual shell quoting is
    /// needed. Fire-and-forget at the process level; gdbus itself completes the
    /// (synchronous) bus call before exiting. Failures are swallowed.
    /// </summary>
    private static void Call(string dest, string objectPath, string method, params string[] methodArgs)
    {
        try
        {
            using var proc = new Process();
            proc.StartInfo.FileName = "gdbus";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;

            var args = proc.StartInfo.ArgumentList;
            args.Add("call");
            args.Add("--session");
            args.Add("--dest"); args.Add(dest);
            args.Add("--object-path"); args.Add(objectPath);
            args.Add("--method"); args.Add(method);
            foreach (var arg in methodArgs) args.Add(arg);

            proc.Start();
        }
        catch
        {
            // best-effort: gdbus missing or no session bus
        }
    }
}
