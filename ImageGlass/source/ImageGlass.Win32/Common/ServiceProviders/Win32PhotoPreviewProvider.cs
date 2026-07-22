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
using ImageGlass.Common;
using ImageGlass.Common.Extensions;
using ImageGlass.Common.Photoing;
using ImageGlass.Common.ServiceProviders;
using SkiaSharp;
using System.Threading;
using System.Threading.Tasks;

namespace ImageGlass.Win32.Common.ServiceProviders;

public class Win32PhotoPreviewProvider : PhotoPreviewProvider
{

    /// <summary>
    /// <inheritdoc/>
    /// Tries to use native platform API to get the shell thumbnail if allowed.
    /// </summary>
    public override async Task<SKImage?> GetPreviewAsync(PhotoMetadata meta, double? minHeight, CancellationToken token = default)
    {
        // 0. if don't use shell thumbnail if not allowed
        if (!Core.Config.EnableGalleryShellThumbnail)
        {
            return await base.GetPreviewAsync(meta, minHeight, token);
        }


        var size = (int)(minHeight ?? double.MinValue);
        var needPreprocess = false;


        // 1. fast path: try Shell cache only (instant, no decoding)
        var imgPreview = await Task.Run(() => Win32ShellThumbnailApi.GetThumbnail(meta.FilePath, size, size, true))
            .ConfigureAwait(false);


        // 2. fast path: native scaled decode via SkiaSharp
        if (imgPreview.IsDisposed())
        {
            imgPreview = await Task.Run(() => SkiaCodec.LoadThumbnail(meta.FilePath, size), token)
                .ConfigureAwait(false);
            needPreprocess = true;
        }


        // 3. try getting thumbnail from Shell
        if (imgPreview.IsDisposed())
        {
            imgPreview = await Task.Run(() => Win32ShellThumbnailApi.GetThumbnail(meta.FilePath, size, size, false))
                .ConfigureAwait(false);
            needPreprocess = false;
        }


        // 4. try embedded EXIF preview
        if (imgPreview.IsDisposed())
        {
            using var thumbM = meta.GetEmbeddedPreview();
            if (thumbM is not null && thumbM.Height >= minHeight)
            {
                imgPreview = SkiaCodec.FromMagick(thumbM, meta.SkiaColorSpace);
                needPreprocess = true;
            }
        }


        // 5. process preview
        if (needPreprocess && TryProcessImage(imgPreview, meta, out var imgProcessed))
        {
            imgPreview?.Dispose();
            imgPreview = imgProcessed;
        }


        if (imgPreview.IsDisposed()) imgPreview = null;
        return imgPreview;
    }


}
