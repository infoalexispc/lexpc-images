namespace LexPCImages.Modules.Optimizer.Application.Abstractions;

public interface IDeskMaskRefiner
{
    MaskResult RemoveDesk(MaskResult mask);
}
