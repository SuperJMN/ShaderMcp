using SkiaSharp;

namespace ShaderMcp.Core;

public static class PixelStatsCalculator
{
    public static PixelStats Calculate(SKBitmap bitmap)
    {
        var count = bitmap.Width * bitmap.Height;
        var nonTransparent = 0;
        var nonBlack = 0;
        var sum = 0.0;
        var sumSquares = 0.0;
        var alphaSum = 0.0;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Alpha > 0)
                    nonTransparent++;

                if (pixel.Red > 0 || pixel.Green > 0 || pixel.Blue > 0)
                    nonBlack++;

                var luminance = (0.2126 * pixel.Red + 0.7152 * pixel.Green + 0.0722 * pixel.Blue) / 255.0;
                sum += luminance;
                sumSquares += luminance * luminance;
                alphaSum += pixel.Alpha / 255.0;
            }
        }

        var mean = count == 0 ? 0 : sum / count;
        var variance = count == 0 ? 0 : sumSquares / count - mean * mean;
        var alpha = count == 0 ? 0 : alphaSum / count;

        return new PixelStats(bitmap.Width, bitmap.Height, nonTransparent, nonBlack, Math.Max(0, variance), alpha);
    }
}
