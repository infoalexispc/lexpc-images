using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Imaging;
using LexPCImages.Modules.Optimizer.Application.Progress;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Application.Pipelines;

/// <summary>
/// Segmenta el producto, refina la máscara según los ajustes del slot, recorta al contenido
/// y estira al tamaño exacto del slot sobre fondo transparente.
/// </summary>
public sealed class BackgroundRemovalPipeline : IImageProcessingPipeline
{
    private readonly IBackgroundRemovalService _backgroundRemover;
    private readonly ILegProtector _legProtector;
    private readonly IDeskMaskRefiner _deskRefiner;
    private readonly IShadowSuppressor _shadowSuppressor;
    private readonly ITightCropper _tightCropper;
    private readonly IImageResizer _resizer;
    private readonly IJobProgressNotifier _notifier;

    public BackgroundRemovalPipeline(
        IBackgroundRemovalService backgroundRemover,
        ILegProtector legProtector,
        IDeskMaskRefiner deskRefiner,
        IShadowSuppressor shadowSuppressor,
        ITightCropper tightCropper,
        IImageResizer resizer,
        IJobProgressNotifier notifier)
    {
        _backgroundRemover = backgroundRemover;
        _legProtector = legProtector;
        _deskRefiner = deskRefiner;
        _shadowSuppressor = shadowSuppressor;
        _tightCropper = tightCropper;
        _resizer = resizer;
        _notifier = notifier;
    }

    public SlotMode Mode => SlotMode.BackgroundRemoval;

    public async Task<DecodedImage> ExecuteAsync(
        ImagePipelineContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (jobId, source, slot, refinement) = context;

        await _notifier.BeginAsync(jobId, OptimizerProgress.Inferring, cancellationToken);
        var mask = await _backgroundRemover.RemoveBackgroundAsync(source, cancellationToken);
        await _notifier.CompleteAsync(jobId, OptimizerProgress.Inferring, cancellationToken);

        mask = await RefineAsync(
            jobId, OptimizerProgress.LegProtecting, refinement.ProtectLegs, mask,
            current => _legProtector.Protect(source, current), cancellationToken);

        mask = await RefineAsync(
            jobId, OptimizerProgress.DeskRemoving, refinement.RemoveDesk, mask,
            _deskRefiner.RemoveDesk, cancellationToken);

        mask = await RefineAsync(
            jobId, OptimizerProgress.ShadowSuppressing, refinement.SuppressShadow, mask,
            current => _shadowSuppressor.Suppress(source, current), cancellationToken);

        await _notifier.BeginAsync(jobId, OptimizerProgress.Cropping, cancellationToken);
        var cropped = _tightCropper.Crop(source, mask, refinement.CropMarginPct);
        await _notifier.CompleteAsync(jobId, OptimizerProgress.Cropping, cancellationToken);

        var masked = MaskCompositor.Apply(cropped.Image, cropped.Mask);

        await _notifier.BeginAsync(jobId, OptimizerProgress.Resizing, cancellationToken);
        var resized = await _resizer.ResizeAsync(
            masked, slot.Width, slot.Height, ResizeMode.Stretch, cancellationToken);
        await _notifier.CompleteAsync(jobId, OptimizerProgress.Resizing, cancellationToken);

        return resized;
    }

    /// <summary>Aplica un refinado opcional notificando su tramo de progreso solo si está activado.</summary>
    private async Task<MaskResult> RefineAsync(
        Guid jobId,
        StageProgress stage,
        bool enabled,
        MaskResult mask,
        Func<MaskResult, MaskResult> refine,
        CancellationToken cancellationToken)
    {
        if (!enabled)
        {
            return mask;
        }

        await _notifier.BeginAsync(jobId, stage, cancellationToken);
        var refined = refine(mask);
        await _notifier.CompleteAsync(jobId, stage, cancellationToken);
        return refined;
    }
}
