using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Domain.Entities;

public sealed class ProcessJob
{
    public const int MaxInputBytes = 15 * 1024 * 1024;
    public const int MinWidth = 200;
    public const int MinHeight = 200;
    public const int MaxWidth = 8000;
    public const int MaxHeight = 8000;

    public Guid Id { get; }
    public SlotDefinition Slot { get; }
    public byte[] InputImage { get; }
    public string InputContentType { get; }
    public JobStatus Status { get; private set; }
    public ProcessingStage? CurrentStage { get; private set; }
    public int Progress { get; private set; }
    public string? OutputContentType { get; private set; }
    public byte[]? OutputImage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private ProcessJob(
        Guid id,
        SlotDefinition slot,
        byte[] inputImage,
        string inputContentType,
        DateTimeOffset createdAt,
        RefinementOptions? refinement)
    {
        Id = id;
        Slot = slot;
        InputImage = inputImage;
        InputContentType = inputContentType;
        Status = JobStatus.Queued;
        Progress = 0;
        CreatedAt = createdAt;
        _refinement = refinement;
    }

    private readonly RefinementOptions? _refinement;

    public RefinementOptions EffectiveRefinement => _refinement ?? Slot.EffectiveRefinement;

    public static ProcessJob Create(
        SlotDefinition slot,
        byte[] inputImage,
        string inputContentType,
        DateTimeOffset now,
        RefinementOptions? refinement = null)
        => new(Guid.NewGuid(), slot, inputImage, inputContentType, now, refinement);

    public void MarkProcessing(ProcessingStage stage, int percent)
    {
        EnsureTransition(JobStatus.Queued, JobStatus.Processing);
        if (percent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "Progress must be 0-100.");
        }
        Status = JobStatus.Processing;
        CurrentStage = stage;
        Progress = percent;
        StartedAt ??= DateTimeOffset.UtcNow;
    }

    public void UpdateProgress(ProcessingStage stage, int percent)
    {
        if (Status != JobStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot update progress of job in status {Status}.");
        }
        if (percent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "Progress must be 0-100.");
        }
        CurrentStage = stage;
        Progress = percent;
    }

    public void MarkDone(byte[] outputImage, string outputContentType)
    {
        EnsureTransition(JobStatus.Queued, JobStatus.Done, JobStatus.Processing, JobStatus.Done);
        if (Status == JobStatus.Done)
        {
            return;
        }
        Status = JobStatus.Done;
        CurrentStage = null;
        Progress = 100;
        OutputImage = outputImage;
        OutputContentType = outputContentType;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkError(string message)
    {
        if (Status == JobStatus.Done)
        {
            throw new InvalidOperationException("Cannot mark an already-done job as error.");
        }
        Status = JobStatus.Error;
        ErrorMessage = message;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureTransition(params JobStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidOperationException(
                $"Invalid transition from {Status}. Allowed: {string.Join(", ", allowed)}.");
        }
    }
}
