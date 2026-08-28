using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Progress;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Application.Pipelines;

/// <summary>
/// Conserva la imagen tal cual: la escala manteniendo la proporción y rellena hasta el tamaño
/// del slot con el color de fondo dominante. No interviene el modelo de segmentación.
/// </summary>
public sealed class ResizeAndPadPipeline : IImageProcessingPipeline
{
    private readonly IImagePadder _padder;
    private readonly IJobProgressNotifier _notifier;

    public ResizeAndPadPipeline(IImagePadder padder, IJobProgressNotifier notifier)
    {
        _padder = padder;
        _notifier = notifier;
    }

    public SlotMode Mode => SlotMode.ResizeAndPad;

    public async Task<DecodedImage> ExecuteAsync(
        ImagePipelineContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await _notifier.BeginAsync(context.JobId, OptimizerProgress.ResizingAndPadding, cancellationToken);
        var padded = _padder.Pad(context.Source, context.Slot.Width, context.Slot.Height);
        await _notifier.CompleteAsync(context.JobId, OptimizerProgress.ResizingAndPadding, cancellationToken);

        return padded.Image;
    }
}
