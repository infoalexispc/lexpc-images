using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Progress;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Application.Pipelines;

/// <summary>
/// Lleva la imagen al tamaño del slot eligiendo entre dos caminos según cuánto se parezcan las
/// proporciones de origen y destino: si el recorte apenas quita nada se escala cubriendo y se
/// recorta centrado; si el recorte mutilaría la imagen se escala entera y las bandas sobrantes se
/// rellenan con el color de fondo dominante. Ambos caminos remuestrean una sola vez.
/// </summary>
public sealed class CoverOrPadPipeline : IImageProcessingPipeline
{
    private readonly IImageResizer _resizer;
    private readonly IImagePadder _padder;
    private readonly IJobProgressNotifier _notifier;

    public CoverOrPadPipeline(IImageResizer resizer, IImagePadder padder, IJobProgressNotifier notifier)
    {
        _resizer = resizer;
        _padder = padder;
        _notifier = notifier;
    }

    public SlotMode Mode => SlotMode.CoverOrPad;

    public async Task<DecodedImage> ExecuteAsync(
        ImagePipelineContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var slot = context.Slot;
        var source = context.Source;

        await _notifier.BeginAsync(context.JobId, OptimizerProgress.Resizing, cancellationToken);

        var shouldCrop = slot.EffectiveCoverFit.ShouldCrop(
            source.Width, source.Height, slot.Width, slot.Height);

        var result = shouldCrop
            ? await _resizer.ResizeAsync(source, slot.Width, slot.Height, ResizeMode.Cover, cancellationToken)
            : _padder.Pad(source, slot.Width, slot.Height).Image;

        await _notifier.CompleteAsync(context.JobId, OptimizerProgress.Resizing, cancellationToken);

        return result;
    }
}
