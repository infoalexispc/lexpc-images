namespace LexPCImages.Modules.Optimizer.Application.Abstractions;

public interface IShadowSuppressor
{
    MaskResult Suppress(DecodedImage original, MaskResult mask);
}
