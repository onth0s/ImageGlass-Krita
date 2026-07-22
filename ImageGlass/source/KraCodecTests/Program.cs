using ImageGlass.Common.Photoing;
using ImageGlass.Common.Types;
using System;
using System.IO;
using System.Threading.Tasks;

namespace KraCodecTests;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== ImageGlass Krita (.kra) Codec Test Suite ===");

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
            Console.WriteLine($"--- Testing: {fileName} ---");

            // 1. Check extension recognition
            bool isKra = KritaCodec.IsKraFile(file);
            Console.WriteLine($"  IsKraFile: {isKra}");
            if (!isKra) { Console.WriteLine("  ❌ Failed extension check!"); continue; }

            // 2. Test Stream Extraction
            using var stream = KritaCodec.OpenPreviewStream(file);
            if (stream == null || stream.Length == 0)
            {
                Console.WriteLine("  ❌ Failed to extract preview stream!");
                continue;
            }
            Console.WriteLine($"  Preview stream extracted: {stream.Length} bytes");

            // 3. Test Metadata Async Loading
            var metadata = await KritaCodec.LoadMetadataAsync(file);
            Console.WriteLine($"  Metadata: Width={metadata.Width}, Height={metadata.Height}, HasAlpha={metadata.HasAlpha}");
            if (metadata.Width == 0 || metadata.Height == 0)
            {
                Console.WriteLine("  ❌ Failed to read valid dimensions!");
                continue;
            }

            // 4. Test Full Decode
            var options = new PhotoReadOptions();
            var decodeOutput = KritaCodec.Load(metadata, options);
            if (decodeOutput.SingleFrame == null)
            {
                Console.WriteLine("  ❌ Decode failed: SingleFrame is null!");
                continue;
            }
            Console.WriteLine($"  Decoded SKImage: Width={decodeOutput.SingleFrame.Width}, Height={decodeOutput.SingleFrame.Height}");

            // 5. Test CodecRegistry Selection
            var selectedCodec = registry.SelectMetadataCodec(file);
            Console.WriteLine($"  CodecRegistry Selected: {selectedCodec?.CodecName ?? "None"} (ID: {selectedCodec?.CodecId ?? "None"})");
            if (selectedCodec is not KritaCodecAdapter)
            {
                Console.WriteLine("  ❌ CodecRegistry did not select KritaCodecAdapter!");
                continue;
            }

            Console.WriteLine("  ✅ PASSED\n");
            passedCount++;
        }

        Console.WriteLine($"=== RESULTS: {passedCount}/{testFiles.Length} Tests Passed ===");
    }
}
