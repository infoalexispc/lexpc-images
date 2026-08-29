using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Progress;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Application.Pipelines;

/// <summary>
/// Escala manteniendo la proporción y deja transparente lo que sobra. Es el modo para imágenes que
/// ya llegan sin fondo: no recorta, no deforma y no inventa un color de relleno que se notaría
/// sobre el alfa del original.
/// </summary>
public sealed class FitTransparentPipeline : IImageProcessingPipeline
{
    private readonly IImageResizer _resizer;
    private readonly IJobProgressNotifier _notifier;

    public FitTransparentPipeline(IImageResizer resizer, IJobProgressNotifier notifier)
    {
        _resizer = resizer;
        _notifier = notifier;
    }

    public SlotMode Mode => SlotMode.FitTransparent;

    public async Task<DecodedImage> ExecuteAsync(
        ImagePipelineContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await _notifier.BeginAsync(context.JobId, OptimizerProgress.Resizing, cancellationToken);
        var fitted = await _resizer.ResizeAsync(
            context.Source,
            context.Slot.Width,
            context.Slot.Height,
            ResizeMode.FitWithTransparentPadding,
            cancellationToken);
        await _notifier.CompleteAsync(context.JobId, OptimizerProgress.Resizing, cancellationToken);

        return fitted;
    }
}
