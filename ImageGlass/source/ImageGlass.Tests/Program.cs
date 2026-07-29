using System;
using System.Collections.Generic;
using System.Reflection;
using Avalonia;
using ImageGlass.Common.Types;
using ImageGlass.UI.Viewer;
using Point = Avalonia.Point;
using Size = Avalonia.Size;

namespace ImageGlass.Tests;

class Program
{
    static int Main(string[] args)
    {
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
}
