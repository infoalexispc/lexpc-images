using LexPCImages.Modules.Optimizer.Application.Abstractions;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

public sealed class ImageSharpTightCropper : ITightCropper
{
    private const float MaskThreshold = 0.5f;

    public CroppedImage Crop(DecodedImage image, MaskResult mask, double marginPct)
    {
        if (image.Width != mask.Width || image.Height != mask.Height)
        {
            throw new InvalidOperationException(
                $"Mask dimensions ({mask.Width}x{mask.Height}) do not match image ({image.Width}x{image.Height}).");
        }
        if (marginPct < 0 || marginPct > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(marginPct), marginPct,
                "marginPct must be in [0, 1].");
        }

        var (minX, maxX, minY, maxY, found) = FindBoundingBox(mask.Values, mask.Width, mask.Height);
        if (!found)
        {
            throw new InvalidOperationException("Cannot crop: mask is empty.");
        }

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var marginX = (int)Math.Round(width * marginPct);
        var marginY = (int)Math.Round(height * marginPct);

        minX = Math.Max(0, minX - marginX);
        maxX = Math.Min(mask.Width - 1, maxX + marginX);
        minY = Math.Max(0, minY - marginY);
        maxY = Math.Min(mask.Height - 1, maxY + marginY);

        var newWidth = maxX - minX + 1;
        var newHeight = maxY - minY + 1;
        var newRgba = new byte[newWidth * newHeight * 4];
        var newMask = new float[newWidth * newHeight];

        for (var y = 0; y < newHeight; y++)
        {
            var srcRow = (y + minY) * image.Width * 4 + minX * 4;
            var srcMaskRow = (y + minY) * mask.Width + minX;
            var dstRow = y * newWidth * 4;
            var dstMaskRow = y * newWidth;
            for (var x = 0; x < newWidth; x++)
            {
                var srcIdx = srcRow + x * 4;
                var dstIdx = dstRow + x * 4;
                newRgba[dstIdx] = image.Rgba[srcIdx];
                newRgba[dstIdx + 1] = image.Rgba[srcIdx + 1];
                newRgba[dstIdx + 2] = image.Rgba[srcIdx + 2];
                newRgba[dstIdx + 3] = image.Rgba[srcIdx + 3];
                newMask[dstMaskRow + x] = mask.Values[srcMaskRow + x];
            }
        }

        return new CroppedImage(
            new DecodedImage(newWidth, newHeight, newRgba),
            new MaskResult(newWidth, newHeight, newMask));
    }

    private static (int MinX, int MaxX, int MinY, int MaxY, bool Found) FindBoundingBox(
        float[] values, int width, int height)
    {
        var minX = int.MaxValue;
        var maxX = -1;
        var minY = int.MaxValue;
        var maxY = -1;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (values[y * width + x] > MaskThreshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        return (minX, maxX, minY, maxY, maxX >= 0);
    }
}
