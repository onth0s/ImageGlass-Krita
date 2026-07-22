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
using ImageGlass.Common.Types;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ImageGlass.Common.Photoing;

/// <summary>
/// Provides Krita (.kra) metadata loading and decoding by extracting the embedded preview PNG.
/// </summary>
public sealed class KritaCodecAdapter : PhDisposable, ICodec
{
    private static readonly string[] _supportedExtensions = [".kra"];


    /// <inheritdoc/>
    public string CodecId { get; } = "krita.skia";

    /// <inheritdoc/>
    public string CodecName { get; } = "Krita (.kra)";

    /// <inheritdoc/>
    public int MetadataPriority { get; } = 200;

    /// <inheritdoc/>
    public int DecodePriority { get; } = 200;

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedExtensions => _supportedExtensions;


    /// <inheritdoc/>
    public bool CanLoadMetadata(string filePath)
    {
        return KritaCodec.IsKraFile(filePath);
    }


    /// <inheritdoc/>
    public Task<PhotoMetadata> LoadMetadataAsync(string filePath,
        PhotoReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return KritaCodec.LoadMetadataAsync(filePath, cancellationToken);
    }


    /// <inheritdoc/>
    public bool CanDecode(PhotoMetadata metadata, CodecSelectionContext context)
    {
        return metadata is not null && KritaCodec.IsKraFile(metadata.FilePath);
    }


    /// <inheritdoc/>
    public async Task<CodecDecodeResult> DecodeAsync(PhotoMetadata metadata,
        PhotoReadOptions options, CodecSelectionContext context, CancellationToken cancellationToken = default)
    {
        using var output = await Task.Run(() => KritaCodec.Load(metadata, options), cancellationToken).ConfigureAwait(false);

        return CodecDecodeResultFactory.FromSkiaOutput(CodecId, output, metadata);
    }
}
