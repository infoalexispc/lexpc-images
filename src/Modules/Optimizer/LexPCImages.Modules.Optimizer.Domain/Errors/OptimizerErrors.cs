using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Shared.Common.Errors;

namespace LexPCImages.Modules.Optimizer.Domain.Errors;

public static class OptimizerErrors
{
    public static readonly Error SlotNotFound = Error.NotFound(
        "optimizer.slot_not_found",
        "The requested slot is not registered.");

    public static readonly Error ImageEmpty = Error.Validation(
        "optimizer.image_empty",
        "The uploaded image is empty.");

    public static readonly Error ImageTooLarge = Error.Validation(
        "optimizer.image_too_large",
        $"The uploaded image exceeds the maximum size of {ProcessJob.MaxInputBytes / 1024 / 1024} MB.");

    public static readonly Error ImageTooSmall = Error.Validation(
        "optimizer.image_too_small",
        $"The uploaded image must be at least {ProcessJob.MinWidth}x{ProcessJob.MinHeight} pixels.");

    public static readonly Error ImageDimensionsTooLarge = Error.Validation(
        "optimizer.image_dimensions_too_large",
        $"The uploaded image must be at most {ProcessJob.MaxWidth}x{ProcessJob.MaxHeight} pixels.");

    public static readonly Error ImageFormatNotSupported = Error.Validation(
        "optimizer.image_format_not_supported",
        "The uploaded image format is not supported (allowed: JPEG, PNG, WebP).");

    public static Error JobNotFound(string jobId) => Error.NotFound(
        "optimizer.job_not_found",
        $"No job with id '{jobId}' was found.");

    public static readonly Error InternalProcessingEnqueueFailed = Error.Internal(
        "optimizer.enqueue_failed",
        "The job was created but could not be enqueued for processing.");
}
