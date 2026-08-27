namespace LexPCImages.Modules.Optimizer.Application.Abstractions;

public interface ILegProtector
{
    MaskResult Protect(DecodedImage original, MaskResult mask);
}
