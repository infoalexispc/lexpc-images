using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging.Internal;

namespace LexPCImages.Modules.Optimizer.Infrastructure.MaskRefinement;

/// <summary>
/// Recorta imagen y máscara a la caja envolvente del contenido, ampliada por un margen relativo.
/// </summary>
public sealed class TightCropper : ITightCropper
{
    private const float MaskThreshold = 0.5f;

    public CroppedImage Crop(DecodedImage image, MaskResult mask, double marginPct)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(mask);
        MaskGeometry.EnsureMatchingDimensions(image, mask);
        if (marginPct is < 0 or > 1 || double.IsNaN(marginPct))
        {
            throw new ArgumentOutOfRangeException(nameof(marginPct), marginPct, "marginPct must be in [0, 1].");
        }

        if (FindBoundingBox(mask) is not { } box)
        {
            throw new InvalidOperationException("Cannot crop: mask is empty.");
        }

        var (minX, maxX, minY, maxY) = ExpandByMargin(box, mask.Width, mask.Height, marginPct);
        var newWidth = maxX - minX + 1;
        var newHeight = maxY - minY + 1;
        var newRgba = new byte[newWidth * newHeight * RgbaImageInterop.BytesPerPixel];
        var newMask = new float[newWidth * newHeight];

        for (var y = 0; y < newHeight; y++)
        {
            var sourceRow = ((y + minY) * image.Width) + minX;
            var destinationRow = y * newWidth;
            Array.Copy(
                image.Rgba,
                sourceRow * RgbaImageInterop.BytesPerPixel,
                newRgba,
                destinationRow * RgbaImageInterop.BytesPerPixel,
                newWidth * RgbaImageInterop.BytesPerPixel);
            Array.Copy(mask.Values, sourceRow, newMask, destinationRow, newWidth);
        }

        return new CroppedImage(
            new DecodedImage(newWidth, newHeight, newRgba),
            new MaskResult(newWidth, newHeight, newMask));
    }

    private static (int MinX, int MaxX, int MinY, int MaxY) ExpandByMargin(
        (int MinX, int MaxX, int MinY, int MaxY) box, int width, int height, double marginPct)
    {
        var marginX = (int)Math.Round((box.MaxX - box.MinX + 1) * marginPct);
        var marginY = (int)Math.Round((box.MaxY - box.MinY + 1) * marginPct);
        return (
            Math.Max(0, box.MinX - marginX),
            Math.Min(width - 1, box.MaxX + marginX),
            Math.Max(0, box.MinY - marginY),
            Math.Min(height - 1, box.MaxY + marginY));
    }

    private static (int MinX, int MaxX, int MinY, int MaxY)? FindBoundingBox(MaskResult mask)
    {
        var minX = int.MaxValue;
        var maxX = -1;
        var minY = int.MaxValue;
        var maxY = -1;
        for (var y = 0; y < mask.Height; y++)
        {
            for (var x = 0; x < mask.Width; x++)
            {
                if (mask.Values[(y * mask.Width) + x] <= MaskThreshold)
                {
                    continue;
                }
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }
        return maxX >= 0 ? (minX, maxX, minY, maxY) : null;
    }
}
