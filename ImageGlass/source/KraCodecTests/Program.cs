using ImageGlass.Common;
using ImageGlass.Common.Photoing;
using ImageGlass.Common.Types;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace KraCodecTests;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("   ImageGlass Fast-Boot Ad-Hoc Target Tests");
        Console.WriteLine("=================================================\n");

        var kraDir = @"c:\Users\Leonardo\001\00__DEV\ImageGlass-Krita\KRA";
        if (!Directory.Exists(kraDir))
        {
            Console.WriteLine($"❌ Test KRA directory not found at: {kraDir}");
            return;
        }

        var testFiles = Directory.GetFiles(kraDir, "*.kra");
        Console.WriteLine($"Found {testFiles.Length} test files in {kraDir}:\n");

        var registry = new CodecRegistry();
        int passedCount = 0;

        foreach (var file in testFiles)
        {
            var fileName = Path.GetFileName(file);
            Console.WriteLine($"--- [Target 1: KRA Codec] Testing: {fileName} ---");

            // 1. Check extension recognition
            bool isKra = KritaCodec.IsKraFile(file);
            if (!isKra) { Console.WriteLine("  ❌ Failed extension check!"); continue; }

            // 2. Test Stream Extraction
            using var stream = KritaCodec.OpenPreviewStream(file);
            if (stream == null || stream.Length == 0)
            {
                Console.WriteLine("  ❌ Failed to extract preview stream!");
                continue;
            }

            // 3. Two-Pass Decode Timing
            var sw2Pass = Stopwatch.StartNew();
            var metadata = await KritaCodec.LoadMetadataAsync(file);
            var options = new PhotoReadOptions();
            var decodeOutput = KritaCodec.Load(metadata, options);
            sw2Pass.Stop();

            if (decodeOutput.SingleFrame == null || metadata.Width == 0)
            {
                Console.WriteLine("  ❌ Two-pass decode failed!");
                continue;
            }

            // 4. Single-Pass Fast Decode Timing (Target 1)
            var sw1Pass = Stopwatch.StartNew();
            var (fastMeta, fastOutput) = KritaCodec.DecodeFast(file, options);
            sw1Pass.Stop();

            if (fastOutput.SingleFrame == null || fastMeta.Width == 0)
            {
                Console.WriteLine("  ❌ Single-pass DecodeFast failed!");
                continue;
            }

            if (fastMeta.Width != metadata.Width || fastMeta.Height != metadata.Height)
            {
                Console.WriteLine($"  ❌ Dimension mismatch between single-pass ({fastMeta.Width}x{fastMeta.Height}) and two-pass ({metadata.Width}x{metadata.Height})!");
                continue;
            }

            Console.WriteLine($"  Two-Pass Decode:   {sw2Pass.ElapsedMilliseconds} ms ({metadata.Width}x{metadata.Height})");
            Console.WriteLine($"  Single-Pass Decode: {sw1Pass.ElapsedMilliseconds} ms ({fastMeta.Width}x{fastMeta.Height})");

            // 5. Test Photo.LoadAsync Pipeline Integration
            var photo = new Photo(file);
            var swPhoto = Stopwatch.StartNew();
            await photo.LoadAsync(useCache: false);
            swPhoto.Stop();

            if (photo.State != PhotoState.Loaded || photo.Bitmap == null)
            {
                Console.WriteLine($"  ❌ Photo.LoadAsync pipeline failed! State={photo.State}");
                continue;
            }
            Console.WriteLine($"  Photo.LoadAsync:   {swPhoto.ElapsedMilliseconds} ms (State={photo.State}, Codec={photo.CodecId})");

            Console.WriteLine("  ✅ PASSED\n");
            passedCount++;
        }

        // Target 2: Config Deserialization Fast-Path Test
        Console.WriteLine("--- [Target 2: Config Load Fast-Path] ---");
        var swConfig = Stopwatch.StartNew();
        var config = Config.Load(Config.CONFIG_USER, []);
        swConfig.Stop();

        bool isConfigValid = config != null && config.FileFormats.Count > 0;
        Console.WriteLine($"  Config.Load Time:  {swConfig.ElapsedMilliseconds} ms (Formats={config?.FileFormats.Count ?? 0})");
        if (isConfigValid)
        {
            Console.WriteLine("  ✅ Config Fast-Path PASSED\n");
        }
        else
        {
            Console.WriteLine("  ❌ Config Fast-Path FAILED\n");
        }

        Console.WriteLine($"=== RESULTS: {passedCount}/{testFiles.Length} KRA Tests Passed, Config Fast-Path {(isConfigValid ? "Passed" : "Failed")} ===");
    }
}
