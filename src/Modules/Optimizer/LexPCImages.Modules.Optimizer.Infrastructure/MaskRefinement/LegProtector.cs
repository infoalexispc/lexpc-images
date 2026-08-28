using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging.Internal;

namespace LexPCImages.Modules.Optimizer.Infrastructure.MaskRefinement;

/// <summary>
/// Recupera estructuras verticales finas —patas de mesa, soportes de monitor— que el
/// segmentador tiende a borrar. Una apertura con kernel alto y estrecho las aísla; se
/// restauran solo donde la imagen original tiene contenido visible.
/// </summary>
public sealed class LegProtector : ILegProtector
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
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(mask);
        MaskGeometry.EnsureMatchingDimensions(original, mask);

        var binary = Morphology.Binarize(mask.Values, OpenThreshold);
        var opened = Morphology.Open(binary, mask.Width, mask.Height, OpenKernelRadiusX, OpenKernelRadiusY);
        var candidates = Morphology.Dilate(opened, mask.Width, mask.Height, CandidatesDilateRadius);

        var recovered = new float[mask.Values.Length];
        for (var i = 0; i < mask.Values.Length; i++)
        {
            recovered[i] = mask.Values[i];
            if (!candidates[i] || mask.Values[i] >= MinMaskAlpha)
            {
                continue;
            }

            var x = i % mask.Width;
            var y = i / mask.Width;
            if (LocalContent(original, x, y) > MinLocalContent)
            {
                recovered[i] = ProtectedAlpha;
            }
        }

        var binarized = Morphology.Binarize(recovered, BinaryThreshold);
        var closed = Morphology.Dilate(binarized, mask.Width, mask.Height, FinalDilateRadius);
        for (var i = 0; i < recovered.Length; i++)
        {
            recovered[i] = closed[i] ? 1f : 0f;
        }

        return new MaskResult(mask.Width, mask.Height, recovered);
    }

    /// <summary>Luminosidad media de la vecindad 3x3 en el original, normalizada a [0, 1].</summary>
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
                var offset = ((sy * image.Width) + sx) * RgbaImageInterop.BytesPerPixel;
                sum += image.Rgba[offset] + image.Rgba[offset + 1] + image.Rgba[offset + 2];
                count += 3;
            }
        }
        return count > 0 ? sum / (float)count / 255f : 0f;
    }
}
