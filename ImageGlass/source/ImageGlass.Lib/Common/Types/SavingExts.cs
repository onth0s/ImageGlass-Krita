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
using Avalonia.Platform.Storage;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ImageGlass.Common.Types;


public static class SavingExts
{
    private static IReadOnlyCollection<KeyValuePair<string, string>> SupportedExtensions =>
    [
        new(".png",   "PNG"),
        new(".jpg",   "JPG"),
        new(".jxl",   "JXL"),
        new(".webp",  "WEBP"),
        new(".avif",  "AVIF"),

        new(".bmp",   "BMP"),
        new(".gif",   "GIF"),
        new(".tiff",  "TIFF"),

        new(".emf",   "EMF"),
        new(".exif",  "EXIF"),
        new(".ico",   "ICO"),
        new(".wmf",   "WMF"),
        new(".b64",   "Base64"),
        new(".txt",   "Base64 text"),
    ];


    /// <summary>
    /// Gets the file picker choices for save file dialog.
    /// </summary>
    public static ImmutableList<FilePickerFileType> FilePickerFileTypeChoices => GetFilterStringForSaveDialog();


    /// <summary>
    /// Gets the map of supported extensions: <c>.extension, description</c>.
    /// </summary>
    public static FrozenDictionary<string, string> ExtensionsMap => new Dictionary<string, string>(SupportedExtensions).ToFrozenDictionary();


    /// <summary>
    /// Gets, sets the last extensions used for saving.
    /// </summary>
    public static FilePickerFileType? LastSavedFileType { get; set; }



    /// <summary>
    /// Returns file picker choices for save file dialog.
    /// </summary>
    private static ImmutableList<FilePickerFileType> GetFilterStringForSaveDialog()
    {
        var list = new List<FilePickerFileType>(SupportedExtensions.Count);

        foreach (var item in SupportedExtensions)
        {
            list.Add(new FilePickerFileType(item.Value)
            {
                Patterns = [$"*{item.Key}"],
            });
        }

        return list.ToImmutableList();
    }

}
