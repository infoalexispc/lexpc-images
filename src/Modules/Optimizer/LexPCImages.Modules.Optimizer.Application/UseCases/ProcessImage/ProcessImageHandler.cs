using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Shared.Common;
using LexPCImages.Shared.Common.Errors;
using Microsoft.Extensions.Logging;

namespace LexPCImages.Modules.Optimizer.Application.UseCases.ProcessImage;

public sealed class ProcessImageHandler
{
    private readonly IImageDecoder _decoder;
    private readonly IBackgroundRemovalService _backgroundRemover;
    private readonly IShadowSuppressor _shadowSuppressor;
    private readonly IDeskMaskRefiner _deskRefiner;
    private readonly ILegProtector _legProtector;
    private readonly ITightCropper _tightCropper;
    private readonly IImagePadder _padder;
    private readonly IImageResizer _resizer;
    private readonly IImageEncoder _encoder;
    private readonly IJobProgressNotifier _notifier;
    private readonly ILogger<ProcessImageHandler> _logger;

    public ProcessImageHandler(
        IImageDecoder decoder,
        IBackgroundRemovalService backgroundRemover,
        IShadowSuppressor shadowSuppressor,
        IDeskMaskRefiner deskRefiner,
        ILegProtector legProtector,
        ITightCropper tightCropper,
        IImagePadder padder,
        IImageResizer resizer,
        IImageEncoder encoder,
        IJobProgressNotifier notifier,
        ILogger<ProcessImageHandler> logger)
    {
        _decoder = decoder;
        _backgroundRemover = backgroundRemover;
        _shadowSuppressor = shadowSuppressor;
        _deskRefiner = deskRefiner;
        _legProtector = legProtector;
        _tightCropper = tightCropper;
        _padder = padder;
        _resizer = resizer;
        _encoder = encoder;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<Result<byte[]>> HandleAsync(ProcessJob job, CancellationToken cancellationToken)
    {
        var slot = job.Slot;
        var refinement = job.EffectiveRefinement;
        _logger.LogInformation(
            "Processing job {JobId} for slot {SlotId} (mode={Mode})",
            job.Id, slot.Id, slot.Mode);

        _notifier.OnStageStarted(job.Id, ProcessingStage.Decoding, 10);
        var decoded = await _decoder.DecodeAsync(job.InputImage, cancellationToken);
        ValidateDimensions(decoded.Width, decoded.Height);
        _notifier.OnStageCompleted(job.Id, ProcessingStage.Decoding, 20);

        var finalImage = slot.Mode switch
        {
            SlotMode.ResizeAndPad => await ResizeAndPadPipelineAsync(
                job, decoded, cancellationToken),
            _ => await BackgroundRemovalPipelineAsync(
                job, decoded, refinement, cancellationToken),
        };

        _notifier.OnStageStarted(job.Id, ProcessingStage.Encoding, 96);
        var webp = await _encoder.EncodeWebPAsync(finalImage, cancellationToken);
        _notifier.OnStageCompleted(job.Id, ProcessingStage.Encoding, 100);

        return Result<byte[]>.Success(webp);
    }

    private async Task<DecodedImage> ResizeAndPadPipelineAsync(
        ProcessJob job,
        DecodedImage decoded,
        CancellationToken cancellationToken)
    {
        _notifier.OnStageStarted(job.Id, ProcessingStage.Resizing, 50);
        var padded = _padder.Pad(decoded, job.Slot.Width, job.Slot.Height);
        _notifier.OnStageCompleted(job.Id, ProcessingStage.Resizing, 90);
        return padded.Image;
    }

    private async Task<DecodedImage> BackgroundRemovalPipelineAsync(
        ProcessJob job,
        DecodedImage decoded,
        RefinementOptions refinement,
        CancellationToken cancellationToken)
    {
        _notifier.OnStageStarted(job.Id, ProcessingStage.Inferring, 25);
        var currentMask = await _backgroundRemover.RemoveBackgroundAsync(decoded, cancellationToken);
        _notifier.OnStageCompleted(job.Id, ProcessingStage.Inferring, 55);

        if (refinement.ProtectLegs)
        {
            _notifier.OnStageStarted(job.Id, ProcessingStage.LegProtecting, 55);
            currentMask = _legProtector.Protect(decoded, currentMask);
            _notifier.OnStageCompleted(job.Id, ProcessingStage.LegProtecting, 62);
        }

        if (refinement.RemoveDesk)
        {
            _notifier.OnStageStarted(job.Id, ProcessingStage.DeskRemoving, 62);
            currentMask = _deskRefiner.RemoveDesk(currentMask);
            _notifier.OnStageCompleted(job.Id, ProcessingStage.DeskRemoving, 70);
        }

        if (refinement.SuppressShadow)
        {
            _notifier.OnStageStarted(job.Id, ProcessingStage.ShadowSuppressing, 70);
            currentMask = _shadowSuppressor.Suppress(decoded, currentMask);
            _notifier.OnStageCompleted(job.Id, ProcessingStage.ShadowSuppressing, 76);
        }

        _notifier.OnStageStarted(job.Id, ProcessingStage.Cropping, 76);
        var cropped = _tightCropper.Crop(decoded, currentMask, refinement.CropMarginPct);
        _notifier.OnStageCompleted(job.Id, ProcessingStage.Cropping, 82);

        var maskedImage = ApplyMask(cropped.Image, cropped.Mask);

        _notifier.OnStageStarted(job.Id, ProcessingStage.Resizing, 82);
        var resized = await _resizer.ResizeAsync(
            maskedImage,
            job.Slot.Width,
            job.Slot.Height,
            ResizeMode.Stretch,
            cancellationToken);
        _notifier.OnStageCompleted(job.Id, ProcessingStage.Resizing, 94);

        return resized;
    }

    private void ValidateDimensions(int width, int height)
    {
        if (width < ProcessJob.MinWidth || height < ProcessJob.MinHeight)
        {
            throw new InvalidOperationException(
                $"Image too small: {width}x{height}. Minimum is {ProcessJob.MinWidth}x{ProcessJob.MinHeight}.");
        }
        if (width > ProcessJob.MaxWidth || height > ProcessJob.MaxHeight)
        {
            throw new InvalidOperationException(
                $"Image too large: {width}x{height}. Maximum is {ProcessJob.MaxWidth}x{ProcessJob.MaxHeight}.");
        }
    }

    private static DecodedImage ApplyMask(DecodedImage image, MaskResult mask)
    {
        if (image.Width != mask.Width || image.Height != mask.Height)
        {
            throw new InvalidOperationException(
                $"Mask dimensions ({mask.Width}x{mask.Height}) do not match image ({image.Width}x{image.Height}).");
        }
        var rgba = new byte[image.Rgba.Length];
        for (var i = 0; i < mask.Values.Length; i++)
        {
            var offset = i * 4;
            rgba[offset] = image.Rgba[offset];
            rgba[offset + 1] = image.Rgba[offset + 1];
            rgba[offset + 2] = image.Rgba[offset + 2];
            var alpha = (byte)Math.Clamp(image.Rgba[offset + 3] * mask.Values[i], 0f, 255f);
            rgba[offset + 3] = alpha;
        }
        return new DecodedImage(image.Width, image.Height, rgba);
    }
}
