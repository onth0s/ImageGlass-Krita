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
using Avalonia;
using ImageGlass.Common.Extensions;
using SkiaSharp;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace ImageGlass.Common.Photoing;

/// <summary>
/// Krita (.kra) file loading via embedded mergedimage.png / preview.png extraction.
/// </summary>
public static class KritaCodec
{
    private static readonly string[] _kraExtensions = [".kra"];

    /// <summary>
    /// Checks if the file is a Krita (.kra) file by extension.
    /// </summary>
    public static bool IsKraFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;

        var ext = Path.GetExtension(filePath);
        foreach (var kraExt in _kraExtensions)
        {
            if (ext.Equals(kraExt, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Opens the .kra ZIP archive and extracts mergedimage.png (or preview.png as fallback)
    /// into a MemoryStream. Returns null if not found or invalid archive.
    /// </summary>
    public static MemoryStream? OpenPreviewStream(string filePath)
    {
        if (!IsKraFile(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            using var fileStream = File.OpenRead(filePath);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);

            // Prefer full-resolution composite canvas; fall back to thumbnail preview
            var entry = archive.GetEntry("mergedimage.png")
                     ?? archive.GetEntry("preview.png");

            if (entry is null) return null;

            var ms = new MemoryStream();
            using (var entryStream = entry.Open())
            {
                entryStream.CopyTo(ms);
            }
            ms.Position = 0;
            return ms;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads Krita-specific metadata (dimensions, orientation, etc.) from the embedded preview PNG.
    /// </summary>
    public static async Task<PhotoMetadata> LoadMetadataAsync(string filePath, CancellationToken token = default)
    {
        var meta = new PhotoMetadata(filePath)
        {
            IsVector = false,
            FrameCount = 1,
            HasAlpha = true,
        };

        await Task.Run(() =>
        {
            try
            {
                using var ms = OpenPreviewStream(filePath);
                if (ms is not null)
                {
                    using var codec = SKCodec.Create(ms);
                    if (codec is not null && !codec.IsDisposed())
                    {
                        meta.OriginalWidth = meta.Width = (uint)codec.Info.Width;
                        meta.OriginalHeight = meta.Height = (uint)codec.Info.Height;
                        meta.HasAlpha = !codec.Info.IsOpaque;
                    }
                }
            }
            catch { }
        }, token).ConfigureAwait(false);

        return meta;
    }

    /// <summary>
    /// Decodes the .kra file's embedded preview stream into a SkiaDecoderOutput.
    /// </summary>
    public static SkiaDecoderOutput Load(PhotoMetadata meta, PhotoReadOptions options)
    {
        using var stream = OpenPreviewStream(meta.FilePath);
        if (stream is null)
        {
            throw new InvalidDataException($"Failed to extract preview image from Krita file: {meta.FilePath}");
        }

        var image = SKImage.FromEncodedData(stream);
        if (image is null || image.Handle == IntPtr.Zero)
        {
            throw new InvalidDataException($"Failed to decode preview PNG from Krita file: {meta.FilePath}");
        }

        return new SkiaDecoderOutput
        {
            Size = new Size(image.Width, image.Height),
            SingleFrame = image
        };
    }
}
