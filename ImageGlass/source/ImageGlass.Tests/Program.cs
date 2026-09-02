using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using ImageGlass.Common;
using ImageGlass.Common.Photoing;
using ImageGlass.Common.ServiceProviders;
using ImageGlass.Common.Types;
using ImageGlass.UI.Viewer;
using SkiaSharp;
using Point = Avalonia.Point;
using Size = Avalonia.Size;

namespace ImageGlass.Tests;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var svgPass = await RunSvgPipelineTestsAsync();
        if (!svgPass)
        {
            Console.WriteLine("SVG PIPELINE TESTS FAILED!");
            return 1;
        }

        var exrPass = await RunExrPipelineTestsAsync();
        if (!exrPass)
        {
            Console.WriteLine("EXR PIPELINE TESTS FAILED!");
            return 1;
        }

        Console.WriteLine("=========================================================================");
        Console.WriteLine("  AUTOMATED DOUBLE-CLICK PIVOT ZOOM & UNIQUE COLOR MATCH TEST SUITE");
        Console.WriteLine("=========================================================================");
        Console.WriteLine();

        int totalTests = 0;
        int passedTests = 0;
        int failedTests = 0;
        int unconstrainedTests = 0;
        int unconstrainedPassed = 0;

        var scenarios = new (string Name, double ImgW, double ImgH, double WinW, double WinH)[]
        {
            ("Large Image (3840x2160) in Standard Viewport (1920x1080)", 3840, 2160, 1920, 1080),
            ("Very Large Image (8000x6000) in Small Viewport (800x600)", 8000, 6000, 800, 600),
            ("Small Image (400x300) in Large Viewport (1920x1080)", 400, 300, 1920, 1080),
            ("Aspect Mismatch (4000x1000) in Square Viewport (1000x1000)", 4000, 1000, 1000, 1000),
            ("Tall Portrait Image (1200x4000) in Landscape Viewport (1920x1080)", 1200, 4000, 1920, 1080)
        };

        var propBitmapSize = typeof(ViewerControl).GetProperty("BitmapSize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var propDrawingArea = typeof(ViewerControl).GetProperty("DrawingArea", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var sc in scenarios)
        {
            Console.WriteLine($"--- Scenario: {sc.Name} ---");

            var testPoints = GenerateTestPoints(sc.WinW, sc.WinH);

            foreach (var pivot in testPoints)
            {
                totalTests++;

                // Fresh viewer state per test point
                var viewer = new ViewerControl();
                propBitmapSize?.SetValue(viewer, new Size(sc.ImgW, sc.ImgH));
                propDrawingArea?.SetValue(viewer, new Rect(0, 0, sc.WinW, sc.WinH));
                viewer.SetZoomMode(ZoomMode.ScaleToFit);

                // 1. Pre-zoom image coordinate & unique color under pivot
                var (preX, preY) = GetImagePoint(viewer, pivot);
                var preColor = GetUniqueColor(preX, preY, sc.ImgW, sc.ImgH);

                // 2. Double-Click Zoom to 100% (Actual Size) using pivot
                viewer.ToggleZoomActualSizeAndFit(centerBothAxes: false, pivotPoint: pivot);

                // 3. Post-zoom image coordinate & unique color under pivot
                var (postX, postY) = GetImagePoint(viewer, pivot);
                var postColor = GetUniqueColor(postX, postY, sc.ImgW, sc.ImgH);

                double driftX = Math.Abs(postX - preX);
                double driftY = Math.Abs(postY - preY);

                double targetZoomFactor = (double)viewer.ZoomFactor / viewer.Dpi;
                bool isClamped = IsEdgeClamped(preX, preY, sc.ImgW, sc.ImgH, sc.WinW, sc.WinH, pivot.X, pivot.Y, targetZoomFactor);

                bool pass = false;
                if (!isClamped)
                {
                    unconstrainedTests++;
                    // Inner unconstrained region: ZERO pixel drift and EXACT color match!
                    bool exactMatch = (driftX < 0.0001 && driftY < 0.0001) &&
                                      (preColor.R == postColor.R && preColor.G == postColor.G && preColor.B == postColor.B);
                    if (exactMatch) unconstrainedPassed++;
                    pass = exactMatch;
                }
                else
                {
                    // Clamped boundary region: coordinate safely clamped within image bounds
                    pass = (postX >= -0.001 && postX <= sc.ImgW + 0.001 && postY >= -0.001 && postY <= sc.ImgH + 0.001);
                }

                // 4. Double-click AGAIN to toggle BACK to fit-to-window
                viewer.ToggleZoomActualSizeAndFit(centerBothAxes: false, pivotPoint: pivot);
                var (cycledX, cycledY) = GetImagePoint(viewer, pivot);
                var cycledColor = GetUniqueColor(cycledX, cycledY, sc.ImgW, sc.ImgH);

                double cycleDriftX = Math.Abs(cycledX - preX);
                double cycleDriftY = Math.Abs(cycledY - preY);
                if (!isClamped && (cycleDriftX > 0.0001 || cycleDriftY > 0.0001 ||
                    preColor.R != cycledColor.R || preColor.G != cycledColor.G || preColor.B != cycledColor.B))
                {
                    pass = false;
                }

                if (pass)
                {
                    passedTests++;
                }
                else
                {
                    failedTests++;
                    Console.WriteLine($"  ❌ [FAIL] Pivot: ({pivot.X:F0}, {pivot.Y:F0}) Clamped={isClamped}");
                    Console.WriteLine($"         Pre-Zoom Img: ({preX:F4}, {preY:F4}) RGB: ({preColor.R},{preColor.G},{preColor.B})");
                    Console.WriteLine($"        Post-Zoom Img: ({postX:F4}, {postY:F4}) RGB: ({postColor.R},{postColor.G},{postColor.B}) [Drift: X={driftX:F4}, Y={driftY:F4}]");
                    Console.WriteLine($"       Cycled     Img: ({cycledX:F4}, {cycledY:F4}) RGB: ({cycledColor.R},{cycledColor.G},{cycledColor.B}) [CycleDrift: X={cycleDriftX:F4}, Y={cycleDriftY:F4}]");
                }
            }

            Console.WriteLine($"  Scenario Completed. All points checked.");
            Console.WriteLine();
        }

        Console.WriteLine("=========================================================================");
        Console.WriteLine($"  SUMMARY RESULTS:");
        Console.WriteLine($"  Total Viewport Points Tested : {totalTests}");
        Console.WriteLine($"  Overall Passed               : {passedTests}");
        Console.WriteLine($"  Overall Failed               : {failedTests}");
        Console.WriteLine($"  Unconstrained Inner Points   : {unconstrainedTests}");
        Console.WriteLine($"  Unconstrained Exact Passed   : {unconstrainedPassed} (Zero Drift & Exact Color Match)");
        Console.WriteLine("=========================================================================");

        return failedTests == 0 ? 0 : 1;
    }

    private static Point[] GenerateTestPoints(double winW, double winH)
    {
        var list = new List<Point>();
        double[] ratios = { 0.1, 0.25, 0.33, 0.5, 0.67, 0.75, 0.9 };
        foreach (var rx in ratios)
        {
            foreach (var ry in ratios)
            {
                list.Add(new Point(winW * rx, winH * ry));
            }
        }
        return list.ToArray();
    }

    private static (double imgX, double imgY) GetImagePoint(ViewerControl viewer, Point screenPoint)
    {
        double factorX = viewer.SrcRect.Width > 0 ? viewer.DestRect.Width / viewer.SrcRect.Width : (double)viewer.ZoomFactor / viewer.Dpi;
        double factorY = viewer.SrcRect.Height > 0 ? viewer.DestRect.Height / viewer.SrcRect.Height : (double)viewer.ZoomFactor / viewer.Dpi;

        var screenX = screenPoint.X - viewer.Padding.Left;
        var screenY = screenPoint.Y - viewer.Padding.Top;

        double imgX = viewer.SrcRect.X + (screenX - viewer.DestRect.X) / factorX;
        double imgY = viewer.SrcRect.Y + (screenY - viewer.DestRect.Y) / factorY;

        return (imgX, imgY);
    }

    private static (byte R, byte G, byte B) GetUniqueColor(double imgX, double imgY, double imgW, double imgH)
    {
        if (imgX < 0 || imgX > imgW || imgY < 0 || imgY > imgH)
        {
            return (0, 0, 0);
        }

        int ix = (int)Math.Floor(imgX);
        int iy = (int)Math.Floor(imgY);

        byte r = (byte)((ix * 17 + 5) & 0xFF);
        byte g = (byte)((iy * 23 + 11) & 0xFF);
        byte b = (byte)(((ix + iy) * 31 + 19) & 0xFF);

        return (r, g, b);
    }

    private static bool IsEdgeClamped(double imgX, double imgY, double imgW, double imgH, double winW, double winH, double screenX, double screenY, double zoomFactor)
    {
        // Clicks outside the image bounds clamp to the nearest image edge
        if (imgX < 0 || imgX > imgW || imgY < 0 || imgY > imgH) return true;

        double scaledW = imgW * zoomFactor;
        double scaledH = imgH * zoomFactor;

        bool xClamped = false;
        if (scaledW > winW)
        {
            double reqSrcX = imgX - screenX / zoomFactor;
            if (reqSrcX < 0 || reqSrcX + winW / zoomFactor > imgW) xClamped = true;
        }

        bool yClamped = false;
        if (scaledH > winH)
        {
            double reqSrcY = imgY - screenY / zoomFactor;
            if (reqSrcY < 0 || reqSrcY + winH / zoomFactor > imgH) yClamped = true;
        }

        return xClamped || yClamped;
    }

    private static async Task<bool> RunSvgPipelineTestsAsync()
    {
        Console.WriteLine("=========================================================================");
        Console.WriteLine("  AUTOMATED SVG VIEWING & THUMBNAIL PIPELINE TEST SUITE");
        Console.WriteLine("=========================================================================");
        Console.WriteLine();

        try
        {
            AppBuilder.Configure<Application>()
                .UsePlatformDetect()
                .SetupWithoutStarting();
            SynchronizationContext.SetSynchronizationContext(null);
        }
        catch { }

        Core.PreviewProvider = new PhotoPreviewProvider();
        Core.Config = new Config { EnableVectorRenderer = true };

        var tempDir = Path.Combine(Path.GetTempPath(), "IG_SvgTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var svg1Path = Path.Combine(tempDir, "test1.svg");
            var svg2Path = Path.Combine(tempDir, "test2.svg");

            File.WriteAllText(svg1Path, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 500 500\" width=\"500\" height=\"500\"><circle cx=\"250\" cy=\"250\" r=\"200\" fill=\"blue\" /></svg>");
            File.WriteAllText(svg2Path, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 800 600\" width=\"800\" height=\"600\"><rect width=\"800\" height=\"600\" fill=\"green\" /></svg>");

            // Test 1: SvgCodec Metadata Loading
            Console.Write("Test 1: SvgCodec.LoadMetadataAsync... ");
            var meta1 = await SvgCodec.LoadMetadataAsync(svg1Path);
            if (!meta1.IsVector || meta1.Width != 500 || meta1.Height != 500)
            {
                Console.WriteLine($"FAILED! Expected 500x500 vector, got {meta1.Width}x{meta1.Height}, isVector={meta1.IsVector}");
                return false;
            }
            Console.WriteLine("PASSED");

            // Test 2: SvgCodec Direct Thumbnail Rasterization
            Console.Write("Test 2: SvgCodec.RasterizeThumbnail... ");
            using (var doc = SvgCodec.LoadSvg(svg1Path))
            {
                if (doc.Picture is null) { Console.WriteLine("FAILED! Picture is null"); return false; }
                using var thumb = SvgCodec.RasterizeThumbnail(doc.Picture, 100);
                if (thumb is null || thumb.Width != 100 || thumb.Height != 100)
                {
                    Console.WriteLine($"FAILED! Expected 100x100 thumb, got {thumb?.Width}x{thumb?.Height}");
                    return false;
                }
            }
            Console.WriteLine("PASSED");

            // Test 3: SkiaCodec.LoadThumbnail for SVG
            Console.Write("Test 3: SkiaCodec.LoadThumbnail for SVG... ");
            using (var thumb = SkiaCodec.LoadThumbnail(svg1Path, 120))
            {
                if (thumb is null || thumb.Width != 120 || thumb.Height != 120)
                {
                    Console.WriteLine($"FAILED! Expected 120x120 thumb, got {thumb?.Width}x{thumb?.Height}");
                    return false;
                }
            }
            Console.WriteLine("PASSED");

            // Test 4: PhotoPreviewProvider.GetThumbnailAsync for SVG
            Console.Write("Test 4: PhotoPreviewProvider.GetThumbnailAsync for SVG... ");
            var previewProvider = new PhotoPreviewProvider();
            using (var thumb = await previewProvider.GetThumbnailAsync(meta1, 80))
            {
                if (thumb is null || thumb.Width <= 0 || thumb.Height <= 0)
                {
                    Console.WriteLine("FAILED! Returned null or empty thumbnail");
                    return false;
                }
            }
            Console.WriteLine("PASSED");

            // Test 5: Photo.LoadThumbnailAsync End-to-End
            Console.Write("Test 5: Photo.LoadThumbnailAsync End-to-End... ");
            var photo1 = new Photo { FilePath = svg1Path };
            await photo1.LoadThumbnailAsync(60, false);
            if (photo1.GalleryThumbnail is null || photo1.GalleryThumbnail.PixelSize.Width <= 0)
            {
                Console.WriteLine("FAILED! Photo.GalleryThumbnail is null or empty");
                return false;
            }
            Console.WriteLine($"PASSED (Thumbnail size: {photo1.GalleryThumbnail.PixelSize.Width}x{photo1.GalleryThumbnail.PixelSize.Height})");

            // Test 6: Vector Navigation & Buffer Retention in ViewerControl
            Console.Write("Test 6: ViewerControl SVG Buffer Retention across Navigation Cycle... ");
            var photo2 = new Photo { FilePath = svg2Path };
            await photo1.LoadAsync(true);
            await photo2.LoadAsync(true);

            if (photo1.Bitmap is not SkiaVectorSource vs1 || photo2.Bitmap is not SkiaVectorSource vs2)
            {
                Console.WriteLine("FAILED! Photo.Bitmap is not SkiaVectorSource");
                return false;
            }

            var viewer = new ViewerControl();
            var handleMethod = typeof(ViewerControl).GetMethod("HandleVectorPhotoLoaded", BindingFlags.NonPublic | BindingFlags.Instance);
            var svgPictureField = typeof(ViewerControl).GetField("_svgPicture", BindingFlags.NonPublic | BindingFlags.Instance);
            var svgDocField = typeof(ViewerControl).GetField("_svgDocument", BindingFlags.NonPublic | BindingFlags.Instance);

            // Step A: Load Photo 1 into Viewer
            handleMethod?.Invoke(viewer, [vs1]);
            if (vs1.SvgDocument is null || vs1.VectorPicture is null)
            {
                Console.WriteLine("FAILED! HandleVectorPhotoLoaded mutated and destroyed vs1!");
                return false;
            }
            if (svgPictureField?.GetValue(viewer) is null)
            {
                Console.WriteLine("FAILED! _svgPicture is null on viewer after loading vs1");
                return false;
            }

            // Step B: Navigate to Photo 2 (unloads photo 1 resources in viewer)
            handleMethod?.Invoke(viewer, [vs2]);
            if (vs1.SvgDocument is null || vs1.VectorPicture is null)
            {
                Console.WriteLine("FAILED! Navigating to vs2 destroyed cached vs1!");
                return false;
            }

            // Step C: Navigate BACK to Photo 1 (cycle back / wrap)
            var handledBack = (bool)handleMethod?.Invoke(viewer, [vs1])!;
            if (!handledBack)
            {
                Console.WriteLine("FAILED! HandleVectorPhotoLoaded returned false when cycling back to vs1!");
                return false;
            }
            if (svgPictureField?.GetValue(viewer) is null || svgDocField?.GetValue(viewer) is null)
            {
                Console.WriteLine("FAILED! _svgPicture or _svgDocument is null after cycling back to vs1!");
                return false;
            }
            Console.WriteLine();
            Console.WriteLine("ALL 6 SVG PIPELINE TESTS PASSED SUCCESSFULLY!");
            Console.WriteLine();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED WITH EXCEPTION: {ex}");
            return false;
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static async Task<bool> RunExrPipelineTestsAsync()
    {
        Console.WriteLine("=========================================================================");
        Console.WriteLine("  AUTOMATED MULTI-CHANNEL & MULTI-LAYER OPENEXR PIPELINE TEST SUITE");
        Console.WriteLine("=========================================================================");
        Console.WriteLine();

        try
        {
            MagickCodec.Initialize();

            // Find scratch dir by walking up from BaseDirectory or CurrentDirectory
            var cur = AppDomain.CurrentDomain.BaseDirectory;
            string? scratchDir = null;
            for (var depth = 0; depth < 10; depth++)
            {
                var candidate = Path.Combine(cur, "scratch");
                if (Directory.Exists(candidate))
                {
                    scratchDir = candidate;
                    break;
                }
                var parent = Directory.GetParent(cur)?.FullName;
                if (parent is null || parent == cur) break;
                cur = parent;
            }

            scratchDir ??= Path.GetFullPath("scratch");

            Console.WriteLine($"Scratch dir: {scratchDir}");

            var testFiles = new[]
            {
                ("toon_light.exr", true), // Blender multi-pass matcap (diffuse.*, specular.*)
                ("check_normal+y.exr", false), // Standard RGB
                ("spec_2.exr", false) // Standard RGBA
            };

            foreach (var (fileName, isMultiPass) in testFiles)
            {
                var filePath = Path.Combine(scratchDir, fileName);
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"⚠️ File not found, skipping: {filePath}");
                    continue;
                }

                Console.Write($"Testing EXR: {fileName}... ");
                var meta = await MagickCodec.LoadMetadataAsync(filePath);
                if (meta.Width <= 0 || meta.Height <= 0)
                {
                    Console.WriteLine($"FAILED! Invalid dimensions {meta.Width}x{meta.Height}");
                    return false;
                }

                var decoded = await MagickCodec.DecodeImageAsync(meta, new PhotoReadOptions(), null, null, default);
                if (decoded.SingleFrame is null)
                {
                    Console.WriteLine("FAILED! DecodeImageAsync returned null SingleFrame");
                    return false;
                }

                using var skImg = SkiaCodec.FromMagick(decoded.SingleFrame, meta.SkiaColorSpace, isHdr: meta.IsHdr);
                if (skImg is null)
                {
                    Console.WriteLine("FAILED! SkiaCodec.FromMagick returned null");
                    return false;
                }

                using var bmp = SKBitmap.FromImage(skImg);
                if (bmp.Width <= 0 || bmp.Height <= 0)
                {
                    Console.WriteLine("FAILED! SKBitmap.FromImage returned empty bitmap");
                    return false;
                }

                // Check that center pixel is not black/corrupted
                var centerColor = bmp.GetPixel(bmp.Width / 2, bmp.Height / 2);
                if (isMultiPass && centerColor.Red == 0 && centerColor.Green == 0 && centerColor.Blue == 0)
                {
                    Console.WriteLine($"FAILED! Multi-pass matcap center pixel is completely black: RGBA({centerColor.Red}, {centerColor.Green}, {centerColor.Blue}, {centerColor.Alpha})");
                    return false;
                }

                // Verify thumbnail generation via PhotoPreviewProvider
                var previewProvider = new PhotoPreviewProvider();
                using var thumbImg = await previewProvider.GetThumbnailAsync(meta, 80);
                if (thumbImg is null)
                {
                    Console.WriteLine("FAILED! PhotoPreviewProvider.GetThumbnailAsync returned null");
                    return false;
                }
                using var thumbBmp = SKBitmap.FromImage(thumbImg);
                var thumbCenter = thumbBmp.GetPixel(thumbBmp.Width / 2, thumbBmp.Height / 2);
                if (isMultiPass && thumbCenter.Red == 0 && thumbCenter.Green == 0 && thumbCenter.Blue == 0)
                {
                    Console.WriteLine($"FAILED! Thumbnail center pixel is completely black: RGBA({thumbCenter.Red}, {thumbCenter.Green}, {thumbCenter.Blue}, {thumbCenter.Alpha})");
                    return false;
                }

                // End-to-end: Photo.LoadThumbnailAsync
                var photo = new Photo { FilePath = filePath };
                await photo.LoadThumbnailAsync(60, useCache: false);
                if (photo.GalleryThumbnail is null)
                {
                    Console.WriteLine("FAILED! photo.GalleryThumbnail is null");
                    return false;
                }
                Console.WriteLine($"  GalleryThumbnail size: {photo.GalleryThumbnail.PixelSize.Width}x{photo.GalleryThumbnail.PixelSize.Height}");

                if (photo.GalleryThumbnail is Avalonia.Media.Imaging.WriteableBitmap wb)
                {
                    using var fb = wb.Lock();
                    unsafe
                    {
                        var ptr = (byte*)fb.Address;
                        var cx = wb.PixelSize.Width / 2;
                        var cy = wb.PixelSize.Height / 2;
                        var pixelOffset = cy * fb.RowBytes + cx * 4;
                        var b = ptr[pixelOffset];
                        var g = ptr[pixelOffset + 1];
                        var r = ptr[pixelOffset + 2];
                        var a = ptr[pixelOffset + 3];
                        Console.WriteLine($"  GalleryThumbnail Center Pixel: BGRA({b}, {g}, {r}, {a})");
                        if (isMultiPass && r == 0 && g == 0 && b == 0)
                        {
                            Console.WriteLine("FAILED! GalleryThumbnail center pixel is BLACK!");
                            return false;
                        }
                    }
                }

                Console.WriteLine($"PASSED (Full: RGBA({centerColor.Red},{centerColor.Green},{centerColor.Blue}), Thumb: RGBA({thumbCenter.Red},{thumbCenter.Green},{thumbCenter.Blue}))");
            }

            Console.WriteLine();
            Console.WriteLine("ALL EXR PIPELINE TESTS PASSED SUCCESSFULLY!");
            Console.WriteLine();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED WITH EXCEPTION: {ex}");
            return false;
        }
    }
}
