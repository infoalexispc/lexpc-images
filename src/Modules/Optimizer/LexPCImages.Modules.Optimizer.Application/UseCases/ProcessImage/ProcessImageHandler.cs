using System.Collections.Frozen;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Pipelines;
using LexPCImages.Modules.Optimizer.Application.Progress;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Application.Errors;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Shared.Common;
using LexPCImages.Shared.Common.Errors;
using Microsoft.Extensions.Logging;

namespace LexPCImages.Modules.Optimizer.Application.UseCases.ProcessImage;

/// <summary>
/// Orquesta el procesado de un trabajo: decodifica, delega en la estrategia del slot y codifica.
/// Las transformaciones concretas viven en las implementaciones de <see cref="IImageProcessingPipeline"/>.
/// </summary>
public sealed class ProcessImageHandler
{
    private readonly IImageDecoder _decoder;
    private readonly IImageEncoder _encoder;
    private readonly IJobProgressNotifier _notifier;
    private readonly FrozenDictionary<SlotMode, IImageProcessingPipeline> _pipelines;
    private readonly ILogger<ProcessImageHandler> _logger;

    public ProcessImageHandler(
        IImageDecoder decoder,
        IImageEncoder encoder,
        IJobProgressNotifier notifier,
        IEnumerable<IImageProcessingPipeline> pipelines,
        ILogger<ProcessImageHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(pipelines);

        _decoder = decoder;
        _encoder = encoder;
        _notifier = notifier;
        _logger = logger;
        _pipelines = BuildRegistry(pipelines);
    }

    public async Task<Result<EncodedImage>> HandleAsync(ProcessJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        var slot = job.Slot;
        _logger.LogInformation(
            "Processing job {JobId} for slot {SlotId} (mode={Mode})", job.Id, slot.Id, slot.Mode);

        if (!_pipelines.TryGetValue(slot.Mode, out var pipeline))
        {
            _logger.LogError("No pipeline registered for slot mode {Mode}", slot.Mode);
            return OptimizerErrors.PipelineNotAvailable(slot.Mode.ToString());
        }

        await _notifier.BeginAsync(job.Id, OptimizerProgress.Decoding, cancellationToken);
        var decoded = await _decoder.DecodeAsync(job.InputImage, cancellationToken);
        if (ValidateDimensions(decoded.Width, decoded.Height) is { } dimensionsError)
        {
            return dimensionsError;
        }
        await _notifier.CompleteAsync(job.Id, OptimizerProgress.Decoding, cancellationToken);

        var context = new ImagePipelineContext(job.Id, decoded, slot);
        var processed = await pipeline.ExecuteAsync(context, cancellationToken);

        await _notifier.BeginAsync(job.Id, OptimizerProgress.Encoding, cancellationToken);
        var encoded = await _encoder.EncodeAsync(processed, cancellationToken);
        await _notifier.CompleteAsync(job.Id, OptimizerProgress.Encoding, cancellationToken);

        return encoded;
    }

    private static FrozenDictionary<SlotMode, IImageProcessingPipeline> BuildRegistry(
        IEnumerable<IImageProcessingPipeline> pipelines)
    {
        var registry = new Dictionary<SlotMode, IImageProcessingPipeline>();
        foreach (var pipeline in pipelines)
        {
            if (!registry.TryAdd(pipeline.Mode, pipeline))
            {
                throw new InvalidOperationException(
                    $"More than one pipeline is registered for slot mode {pipeline.Mode}: " +
                    $"{registry[pipeline.Mode].GetType().Name} and {pipeline.GetType().Name}.");
            }
        }
        return registry.ToFrozenDictionary();
    }

    private static Error? ValidateDimensions(int width, int height)
    {
        if (width < ProcessJob.MinWidth || height < ProcessJob.MinHeight)
        {
            return OptimizerErrors.ImageTooSmall;
        }
        if (width > ProcessJob.MaxWidth || height > ProcessJob.MaxHeight)
        {
            return OptimizerErrors.ImageDimensionsTooLarge;
        }
        return null;
    }
}
