using SkiaSharp;

// Default output directory
const string DefaultOutputDir = @"c:\Users\Leonardo\001\00__DEV\ImageGlass-Krita\KRA\test_shared_zoom\";

string outputDir = args.Length > 0 ? args[0] : DefaultOutputDir;
Directory.CreateDirectory(outputDir);

// Image definitions: (filename, width, height)
var images = new (string Name, int W, int H)[]
{
    ("A_large_landscape.png",  4000, 3000),
    ("B_small_landscape.png",   800,  600),
    ("C_small_portrait.png",    600,  800),
    ("D_square.png",           1200, 1200),
};

foreach (var (name, W, H) in images)
{
    using var bitmap = new SKBitmap(W, H, SKColorType.Rgba8888, SKAlphaType.Opaque);

    for (int y = 0; y < H; y++)
    {
        for (int x = 0; x < W; x++)
        {
            byte r = (byte)(x * 255 / (W - 1));
            byte g = (byte)(y * 255 / (H - 1));
            const byte b = 128;
            bitmap.SetPixel(x, y, new SKColor(r, g, b));
        }
    }

    string filePath = Path.Combine(outputDir, name);

    using var image = SKImage.FromBitmap(bitmap);
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    using var stream = File.OpenWrite(filePath);
    data.SaveTo(stream);

    Console.WriteLine(filePath);
}
