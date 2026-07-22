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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ImageGlass.Common.Photoing;

/// <summary>
/// Provides a common contract for built-in and future external photo codecs.
/// </summary>
public interface ICodec : IDisposable
{
    /// <summary>
    /// Gets the stable codec identifier.
    /// </summary>
    string CodecId { get; }

    /// <summary>
    /// Gets the friendly name of codec.
    /// </summary>
    string CodecName { get; }

    /// <summary>
    /// Gets the ordering priority when selecting a metadata codec.
    /// Higher values are evaluated first.
    /// </summary>
    int MetadataPriority { get; }

    /// <summary>
    /// Gets the ordering priority when selecting a decode codec.
    /// Higher values are evaluated first.
    /// </summary>
    int DecodePriority { get; }

    /// <summary>
    /// Gets the known extensions for the codec.
    /// This is informational only for now.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Returns <c>true</c> if this codec can load metadata for the specified file.
    /// </summary>
    bool CanLoadMetadata(string filePath);

    /// <summary>
    /// Loads metadata for the specified file.
    /// </summary>
    Task<PhotoMetadata> LoadMetadataAsync(string filePath,
        PhotoReadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> if this codec can decode the supplied metadata under the current runtime context.
    /// </summary>
    bool CanDecode(PhotoMetadata metadata, CodecSelectionContext context);

    /// <summary>
    /// Decodes the supplied metadata into a viewer-compatible result.
    /// </summary>
    Task<CodecDecodeResult> DecodeAsync(PhotoMetadata metadata,
        PhotoReadOptions options,
        CodecSelectionContext context,
        CancellationToken cancellationToken = default);
}