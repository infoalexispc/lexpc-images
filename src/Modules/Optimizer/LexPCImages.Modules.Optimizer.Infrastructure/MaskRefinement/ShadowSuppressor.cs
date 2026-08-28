using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging.Internal;

namespace LexPCImages.Modules.Optimizer.Infrastructure.MaskRefinement;

/// <summary>
/// Atenúa las zonas de la máscara que parecen sombra: píxeles poco saturados y de luminosidad
/// media que el segmentador ha incluido con alfa bajo. Las sombras propias del producto
/// (rodeadas de máscara sólida) se atenúan; las proyectadas sobre la mesa se eliminan.
/// </summary>
public sealed class ShadowSuppressor : IShadowSuppressor
{
    private const float MinMaskAlpha = 0.1f;
    private const float MaxShadowSaturation = 0.25f;
    private const float MinShadowValue = 0.15f;
    private const float MaxShadowValue = 0.55f;
    private const float FormShadowAlphaMultiplier = 0.4f;
    private const float CastShadowAlphaMultiplier = 0.0f;
    private const float LocalAlphaThreshold = 0.5f;
    private const float ProtectedAlphaThreshold = 0.5f;
    private const int ErodeRadius = 1;

    public MaskResult Suppress(DecodedImage original, MaskResult mask)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(mask);
        MaskGeometry.EnsureMatchingDimensions(original, mask);

        var suspect = BuildSuspectMask(original, mask);
        var eroded = Morphology.ErodeGrayscale(suspect, mask.Width, mask.Height, ErodeRadius);

        var result = new float[mask.Values.Length];
        for (var i = 0; i < mask.Values.Length; i++)
        {
            // Núcleo sólido del producto: intocable.
            if (mask.Values[i] >= ProtectedAlphaThreshold || eroded[i] < 0.5f)
            {
                result[i] = mask.Values[i];
                continue;
            }

            var localAlpha = ComputeLocalAlpha(mask.Values, i, mask.Width, mask.Height);
            var multiplier = localAlpha >= LocalAlphaThreshold
                ? FormShadowAlphaMultiplier
                : CastShadowAlphaMultiplier;
            result[i] = mask.Values[i] * multiplier;
        }

        return new MaskResult(mask.Width, mask.Height, result);
    }

    private static float[] BuildSuspectMask(DecodedImage original, MaskResult mask)
    {
        var suspect = new float[mask.Values.Length];
        for (var i = 0; i < mask.Values.Length; i++)
        {
            if (mask.Values[i] < MinMaskAlpha)
            {
                continue;
            }

            var offset = i * RgbaImageInterop.BytesPerPixel;
            var r = original.Rgba[offset] / 255f;
            var g = original.Rgba[offset + 1] / 255f;
            var b = original.Rgba[offset + 2] / 255f;

            var max = MathF.Max(r, MathF.Max(g, b));
            var min = MathF.Min(r, MathF.Min(g, b));
            var value = max;
            var saturation = max == 0f ? 0f : (max - min) / max;

            if (saturation < MaxShadowSaturation && value >= MinShadowValue && value <= MaxShadowValue)
            {
                suspect[i] = 1f;
            }
        }
        return suspect;
    }

    private static float ComputeLocalAlpha(float[] values, int index, int width, int height)
    {
        var x = index % width;
        var y = index / width;
        var sum = 0f;
        var count = 0;
        for (var dy = -1; dy <= 1; dy++)
        {
            var sy = y + dy;
            if ((uint)sy >= (uint)height)
            {
                continue;
            }
            for (var dx = -1; dx <= 1; dx++)
            {
                var sx = x + dx;
                if ((uint)sx >= (uint)width)
                {
                    continue;
                }
                sum += values[(sy * width) + sx];
                count++;
            }
        }
        return count > 0 ? sum / count : 0f;
    }
}
