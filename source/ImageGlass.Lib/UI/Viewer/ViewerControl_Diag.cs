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
using Avalonia.Threading;
using ImageGlass.Common;
using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImageGlass.UI.Viewer;

/// <summary>
/// Diagnostic named-pipe server for programmatic testing of zoom/pan state.
/// Active only when ImageGlass is launched with the <c>--diag</c> flag.
/// Pipe name: <c>imageglass_diag</c>
///
/// Protocol (one command per connection, UTF-8, terminated by \n):
///   QUERY                    → JSON state snapshot
///   SET_ZOOM {ratio}         → set SharedZoomRatio, recalculate, respond "OK"
///   SET_PAN {normX} {normY}  → set SharedZoomPanNorm, recalculate, respond "OK"
///   NAVIGATE {delta}         → request image navigation by delta (+1/-1), respond "OK"
/// </summary>
public partial class ViewerControl
{
    public const string DIAG_PIPE_NAME = "imageglass_diag";
    private CancellationTokenSource? _diagCts;

    /// <summary>
    /// Raised by the diag pipe when a NAVIGATE command is received.
    /// The integer argument is the navigation delta (+1 = next, -1 = previous).
    /// </summary>
    public event Action<int>? DiagNavigateRequested;

    /// <summary>
    /// Starts the diagnostic pipe server if --diag flag is present in CLI args.
    /// Called once from MainWindow_View OnLoaded.
    /// </summary>
    internal void StartDiagServer()
    {
        var args = Environment.GetCommandLineArgs();
        var isDiag = Array.Exists(args, a =>
            string.Equals(a.Trim('"', '\''), "--diag", StringComparison.OrdinalIgnoreCase));

        if (!isDiag) return;

        _diagCts = new CancellationTokenSource();
        var token = _diagCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(
                        DIAG_PIPE_NAME,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) break;

                    var buffer = new byte[512];
                    var bytesRead = await pipe.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead > 0)
                    {
                        var cmd = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        var response = await HandleDiagCommandAsync(cmd, token).ConfigureAwait(false);
                        var respBytes = Encoding.UTF8.GetBytes(response + "\n");
                        await pipe.WriteAsync(respBytes, 0, respBytes.Length, token).ConfigureAwait(false);
                        await pipe.FlushAsync(token).ConfigureAwait(false);
                    }

                    if (OperatingSystem.IsWindows()) pipe.WaitForPipeDrain();
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DiagPipe Error] {ex}");
                }
            }
        }, token);
    }


    /// <summary>Stops the diagnostic pipe server.</summary>
    internal void StopDiagServer()
    {
        _diagCts?.Cancel();
        _diagCts?.Dispose();
        _diagCts = null;
    }


    /// <summary>
    /// Parses and dispatches a diag command. All viewer state mutations run on the UI thread.
    /// </summary>
    private Task<string> HandleDiagCommandAsync(string cmd, CancellationToken token)
    {
        return Dispatcher.UIThread.InvokeAsync(() =>
        {
            var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var verb = parts.Length > 0 ? parts[0].ToUpperInvariant() : "";

            switch (verb)
            {
                case "QUERY":
                    return BuildDiagJson();

                case "SET_ZOOM":
                    // SET_ZOOM {ratio}
                    if (parts.Length >= 2
                        && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var ratio)
                        && BitmapSize.Width > 0)
                    {
                        var fitFactor = CalculateZoomFactor(ZoomMode.ScaleToFit, BitmapSize.Width, BitmapSize.Height);
                        var newFactor = ratio * fitFactor;
                        SetZoomFactor(newFactor, isManualZoom: true);
                        return "OK";
                    }
                    return "ERR";

                case "SET_PAN":
                    // SET_PAN {normX} {normY}  — normX/Y are center-image fractions [0,1]
                    if (parts.Length >= 3
                        && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var normX)
                        && double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var normY)
                        && BitmapSize.Width > 0)
                    {
                        var zFactor = _zooming.Factor / Dpi;
                        var controlW = DrawingArea.Width > 0 ? DrawingArea.Width : 800;
                        var controlH = DrawingArea.Height > 0 ? DrawingArea.Height : 600;
                        var halfViewW = controlW / (2.0 * zFactor);
                        var halfViewH = controlH / (2.0 * zFactor);

                        _sharedZoomPanNormX = Math.Clamp(normX, 0, 1);
                        _sharedZoomPanNormY = Math.Clamp(normY, 0, 1);

                        _logicalSrcPoint = new Avalonia.Point(
                            _sharedZoomPanNormX * BitmapSize.Width  - halfViewW,
                            _sharedZoomPanNormY * BitmapSize.Height - halfViewH);

                        CalculateDrawingRegion();
                        InvalidateVisual();
                        return "OK";
                    }
                    return "ERR";

                case "NAVIGATE":
                    // NAVIGATE {delta}
                    if (parts.Length >= 2
                        && int.TryParse(parts[1], out var delta))
                    {
                        DiagNavigateRequested?.Invoke(delta);
                        return "OK";
                    }
                    return "ERR";

                default:
                    return $"ERR unknown command: {verb}";
            }
        }).GetTask();
    }


    /// <summary>Serialises the current viewer transform state to a single-line JSON string.</summary>
    private string BuildDiagJson()
    {
        var bW = BitmapSize.Width;
        var bH = BitmapSize.Height;
        var vW = DrawingArea.Width;
        var vH = DrawingArea.Height;
        var zf = _zooming.Factor;
        var srcX = SrcRect.X;
        var srcY = SrcRect.Y;
        var srcW = SrcRect.Width;
        var srcH = SrcRect.Height;
        var destX = DestRect.X;
        var destY = DestRect.Y;
        var destW = DestRect.Width;
        var destH = DestRect.Height;
        var lspX = _logicalSrcPoint.X;
        var lspY = _logicalSrcPoint.Y;
        var ratio = _sharedZoomRatio ?? double.NaN;
        var panX = _sharedZoomPanNormX;
        var panY = _sharedZoomPanNormY;
        var dpi = Dpi;
        var filePath = Photo?.FilePath ?? string.Empty;
        var sharedZoomEnabled = Core.Config.EnableSharedZoom;

        return $"{{" +
               $"\"FilePath\":\"{Esc(filePath)}\"," +
               $"\"BitmapWidth\":{bW}," +
               $"\"BitmapHeight\":{bH}," +
               $"\"ViewportWidth\":{F(vW)}," +
               $"\"ViewportHeight\":{F(vH)}," +
               $"\"Dpi\":{F(dpi)}," +
               $"\"ZoomFactor\":{F(zf)}," +
               $"\"SrcX\":{F(srcX)}," +
               $"\"SrcY\":{F(srcY)}," +
               $"\"SrcWidth\":{F(srcW)}," +
               $"\"SrcHeight\":{F(srcH)}," +
               $"\"DestX\":{F(destX)}," +
               $"\"DestY\":{F(destY)}," +
               $"\"DestWidth\":{F(destW)}," +
               $"\"DestHeight\":{F(destH)}," +
               $"\"LogicalSrcX\":{F(lspX)}," +
               $"\"LogicalSrcY\":{F(lspY)}," +
               $"\"SharedZoomEnabled\":{(sharedZoomEnabled ? "true" : "false")}," +
               $"\"SharedZoomRatio\":{F(ratio)}," +
               $"\"SharedZoomPanNormX\":{F(panX)}," +
               $"\"SharedZoomPanNormY\":{F(panY)}" +
               $"}}";

        static string F(double v) => double.IsNaN(v) ? "null" : v.ToString("G10", CultureInfo.InvariantCulture);
        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
