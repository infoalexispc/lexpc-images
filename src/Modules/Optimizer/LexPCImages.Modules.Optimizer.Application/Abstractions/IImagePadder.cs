namespace LexPCImages.Modules.Optimizer.Application.Abstractions;

public interface IImagePadder
{
    PaddedImage Pad(DecodedImage image, int targetWidth, int targetHeight);
}

public sealed record PaddedImage(DecodedImage Image, int OffsetX, int OffsetY);
