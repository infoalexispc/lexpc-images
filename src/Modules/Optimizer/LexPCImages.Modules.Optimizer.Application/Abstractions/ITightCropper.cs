namespace LexPCImages.Modules.Optimizer.Application.Abstractions;

public interface ITightCropper
{
    CroppedImage Crop(DecodedImage image, MaskResult mask, double marginPct);
}

public sealed record CroppedImage(DecodedImage Image, MaskResult Mask);
