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
using ImageGlass.Common.Types;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ImageGlass.Common.Photoing;

/// <summary>
/// Provides Magick-based metadata loading and decode fallback.
/// </summary>
public sealed class MagickCodecAdapter : PhDisposable, ICodec
{
    private static readonly string[] _supportedExtensions = [];


    /// <inheritdoc/>
    public string CodecId { get; } = "magick.net";

    /// <inheritdoc/>
    public string CodecName { get; } = "Magick.NET";

    /// <inheritdoc/>
    public int MetadataPriority { get; } = 100;

    /// <inheritdoc/>
    public int DecodePriority { get; } = 10;

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedExtensions => _supportedExtensions;


    /// <inheritdoc/>
    public bool CanLoadMetadata(string filePath)
    {
        return !string.IsNullOrWhiteSpace(filePath)
            && MagickCodec.CanRead(filePath);
    }


    /// <inheritdoc/>
    public Task<PhotoMetadata> LoadMetadataAsync(string filePath,
        PhotoReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return MagickCodec.LoadMetadataAsync(filePath, options, null, cancellationToken);
    }


    /// <inheritdoc/>
    public bool CanDecode(PhotoMetadata metadata, CodecSelectionContext context)
    {
        return !string.IsNullOrWhiteSpace(metadata?.FilePath)
            && MagickCodec.CanRead(metadata.FilePath);
    }


    /// <inheritdoc/>
    public async Task<CodecDecodeResult> DecodeAsync(PhotoMetadata metadata,
        PhotoReadOptions options,
        CodecSelectionContext context,
        CancellationToken cancellationToken = default)
    {
        using var output = await MagickCodec.DecodeImageAsync(
            metadata,
            options,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        var image = SkiaCodec.FromMagick(output.SingleFrame, metadata.SkiaColorSpace, metadata.IsHdr);

        return new CodecDecodeResult
        {
            CodecId = CodecId,
            ContentKind = image is not null ? CodecContentKind.StaticRaster : CodecContentKind.None,
            Size = new Size(image?.Width ?? 0, image?.Height ?? 0),
            SingleFrame = image,
            IsHdr = metadata.IsHdr,
            HasEmbeddedColorProfile = metadata.SkiaColorSpace is not null || metadata.MagickColorProfile is not null,
        };
    }
}