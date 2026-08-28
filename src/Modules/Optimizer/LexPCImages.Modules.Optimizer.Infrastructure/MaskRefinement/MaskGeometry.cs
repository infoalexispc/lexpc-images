using LexPCImages.Modules.Optimizer.Application.Abstractions;

namespace LexPCImages.Modules.Optimizer.Infrastructure.MaskRefinement;

/// <summary>Comprobaciones de forma compartidas por los refinadores de máscara.</summary>
internal static class MaskGeometry
{
    public static void EnsureMatchingDimensions(DecodedImage image, MaskResult mask)
    {
        if (image.Width != mask.Width || image.Height != mask.Height)
        {
            throw new InvalidOperationException(
                $"Mask dimensions ({mask.Width}x{mask.Height}) do not match image ({image.Width}x{image.Height}).");
        }
    }
}
