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
using ImageGlass.Common.ServiceProviders;
using System;
using System.Linq;

namespace ImageGlass.Mac.Common.ServiceProviders;

internal class MacShareProvider : IShareProvider
{

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public void ShowShare(nint windowHandle, string[] filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        if (filePaths.Length == 0) return;

        // build a comma-separated list of POSIX file references for AppleScript
        var fileList = string.Join(", ",
            filePaths.Select(f => $"(POSIX file \"{f}\" as alias)"));

        // reveal and select the files in Finder, then trigger the Share menu
        MacShellProvider.RunAppleScript(
            "tell application \"Finder\"\n" +
            $"select {{{fileList}}}\n" +
            "activate\n" +
            "end tell\n" +
            "tell application \"System Events\"\n" +
            "tell process \"Finder\"\n" +
            "click menu item \"Share…\" of menu \"File\" of menu bar 1\n" +
            "end tell\n" +
            "end tell");
    }

}
