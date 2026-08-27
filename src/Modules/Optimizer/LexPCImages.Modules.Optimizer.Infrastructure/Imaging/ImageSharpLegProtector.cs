using LexPCImages.Modules.Optimizer.Application.Abstractions;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

public sealed class ImageSharpLegProtector : ILegProtector
{
    private const int OpenKernelRadiusX = 0;
    private const int OpenKernelRadiusY = 3;
    private const int CandidatesDilateRadius = 1;
    private const int FinalDilateRadius = 2;
    private const float MinMaskAlpha = 0.15f;
    private const float MinLocalContent = 0.15f;
    private const float ProtectedAlpha = 0.9f;
    private const float OpenThreshold = 0.05f;
    private const float BinaryThreshold = 128f / 255f;

    public MaskResult Protect(DecodedImage original, MaskResult mask)
    {
        if (original.Width != mask.Width || original.Height != mask.Height)
        {
            throw new InvalidOperationException(
                $"Mask dimensions ({mask.Width}x{mask.Height}) do not match image ({original.Width}x{original.Height}).");
        }

        var binary = Binarize(mask.Values, OpenThreshold);
        var opened = OpenVertical(binary, mask.Width, mask.Height);
        var candidates = Dilate(opened, mask.Width, mask.Height, CandidatesDilateRadius);

        var recovered = new float[mask.Values.Length];
        for (var i = 0; i < mask.Values.Length; i++)
        {
            recovered[i] = mask.Values[i];
            if (candidates[i] && mask.Values[i] < MinMaskAlpha)
            {
                var x = i % mask.Width;
                var y = i / mask.Width;
                if (LocalContent(original, x, y) > MinLocalContent)
                {
                    recovered[i] = ProtectedAlpha;
                }
            }
        }

        var binarized = Binarize(recovered, BinaryThreshold);
        var closed = Dilate(binarized, mask.Width, mask.Height, FinalDilateRadius);
        for (var i = 0; i < recovered.Length; i++)
        {
            recovered[i] = closed[i] ? 1f : 0f;
        }

        return new MaskResult(mask.Width, mask.Height, recovered);
    }

    private static float LocalContent(DecodedImage image, int x, int y)
    {
        var sum = 0L;
        var count = 0;
        for (var dy = -1; dy <= 1; dy++)
        {
            var sy = y + dy;
            if ((uint)sy >= (uint)image.Height)
            {
                continue;
            }
            for (var dx = -1; dx <= 1; dx++)
            {
                var sx = x + dx;
                if ((uint)sx >= (uint)image.Width)
                {
                    continue;
                }
                var offset = (sy * image.Width + sx) * 4;
                sum += image.Rgba[offset] + image.Rgba[offset + 1] + image.Rgba[offset + 2];
                count += 3;
            }
        }
        return count > 0 ? (sum / (float)count) / 255f : 0f;
    }

    private static bool[] Binarize(float[] values, float threshold)
    {
        var result = new bool[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            result[i] = values[i] >= threshold;
        }
        return result;
    }

    private static bool[] OpenVertical(bool[] values, int width, int height)
    {
        var eroded = Erode(values, width, height, OpenKernelRadiusX, OpenKernelRadiusY);
        return Dilate(eroded, width, height, OpenKernelRadiusX, OpenKernelRadiusY);
    }

    private static bool[] Dilate(bool[] values, int width, int height, int radius) =>
        Dilate(values, width, height, radius, radius);

    private static bool[] Dilate(bool[] values, int width, int height, int radiusX, int radiusY)
    {
        var result = new bool[values.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var any = false;
                for (var ky = -radiusY; ky <= radiusY && !any; ky++)
                {
                    var sy = y + ky;
                    if ((uint)sy >= (uint)height)
                    {
                        continue;
                    }
                    for (var kx = -radiusX; kx <= radiusX && !any; kx++)
                    {
                        var sx = x + kx;
                        if ((uint)sx >= (uint)width)
                        {
                            continue;
                        }
                        if (values[sy * width + sx])
                        {
                            any = true;
                        }
                    }
                }
                result[y * width + x] = any;
            }
        }
        return result;
    }

    private static bool[] Erode(bool[] values, int width, int height, int radiusX, int radiusY)
    {
        var result = new bool[values.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var all = true;
                for (var ky = -radiusY; ky <= radiusY && all; ky++)
                {
                    var sy = y + ky;
                    if ((uint)sy >= (uint)height)
                    {
                        all = false;
                        continue;
                    }
                    for (var kx = -radiusX; kx <= radiusX && all; kx++)
                    {
                        var sx = x + kx;
                        if ((uint)sx >= (uint)width)
                        {
                            all = false;
                            continue;
                        }
                        if (!values[sy * width + sx])
                        {
                            all = false;
                        }
                    }
                }
                result[y * width + x] = all;
            }
        }
        return result;
    }
}
