using FluentAssertions;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.UnitTests.Optimizer.Domain;

public sealed class ProcessJobTests
{
    private static readonly SlotDefinition Slot = SlotDefinition.PcHome;
    private static readonly byte[] AnyImage = new byte[] { 0x01, 0x02, 0x03 };

    [Fact]
    public void Create_starts_in_Queued_status_with_zero_progress()
    {
        var job = ProcessJob.Create(Slot, AnyImage, "image/png", DateTimeOffset.UtcNow);

        job.Status.Should().Be(JobStatus.Queued);
        job.Progress.Should().Be(0);
        job.CurrentStage.Should().BeNull();
        job.StartedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void MarkProcessing_transitions_to_Processing_with_stage_and_progress()
    {
        var job = ProcessJob.Create(Slot, AnyImage, "image/png", DateTimeOffset.UtcNow);

        job.MarkProcessing(ProcessingStage.Decoding, 5);

        job.Status.Should().Be(JobStatus.Processing);
        job.CurrentStage.Should().Be(ProcessingStage.Decoding);
        job.Progress.Should().Be(5);
        job.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkProcessing_rejects_invalid_progress()
    {
        var job = ProcessJob.Create(Slot, AnyImage, "image/png", DateTimeOffset.UtcNow);

        var act = () => job.MarkProcessing(ProcessingStage.Decoding, -1);
        act.Should().Throw<ArgumentOutOfRangeException>();

        var act2 = () => job.MarkProcessing(ProcessingStage.Decoding, 101);
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateProgress_only_works_in_Processing_status()
    {
        var job = ProcessJob.Create(Slot, AnyImage, "image/png", DateTimeOffset.UtcNow);

        var act = () => job.UpdateProgress(ProcessingStage.Decoding, 10);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot update progress*");
    }

    [Fact]
    public void UpdateProgress_advances_stage_and_progress()
    {
        var job = ProcessJob.Create(Slot, AnyImage, "image/png", DateTimeOffset.UtcNow);
        job.MarkProcessing(ProcessingStage.Decoding, 5);

        job.UpdateProgress(ProcessingStage.Inferring, 50);

        job.CurrentStage.Should().Be(ProcessingStage.Inferring);
        job.Progress.Should().Be(50);
    }

    [Fact]
    public void MarkDone_sets_status_Done_with_output()
    {
        var job = ProcessJob.Create(Slot, AnyImage, "image/png", DateTimeOffset.UtcNow);
        job.MarkProcessing(ProcessingStage.Decoding, 5);

        job.MarkDone(new byte[] { 0xFF, 0xFE }, "image/webp");

        job.Status.Should().Be(JobStatus.Done);
        job.Progress.Should().Be(100);
        job.CurrentStage.Should().BeNull();
        job.OutputImage.Should().NotBeNull();
        job.OutputContentType.Should().Be("image/webp");
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkError_sets_status_Error_with_message()
    {
        var job = ProcessJob.Create(Slot, AnyImage, "image/png", DateTimeOffset.UtcNow);
        job.MarkProcessing(ProcessingStage.Decoding, 5);

        job.MarkError("model not loaded");

        job.Status.Should().Be(JobStatus.Error);
        job.ErrorMessage.Should().Be("model not loaded");
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkError_after_Done_throws()
    {
        var job = ProcessJob.Create(Slot, AnyImage, "image/png", DateTimeOffset.UtcNow);
        job.MarkProcessing(ProcessingStage.Decoding, 5);
        job.MarkDone(new byte[] { 0xFF }, "image/webp");

        var act = () => job.MarkError("late error");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkProcessing_after_Done_throws()
    {
        var job = ProcessJob.Create(Slot, AnyImage, "image/png", DateTimeOffset.UtcNow);
        job.MarkProcessing(ProcessingStage.Decoding, 5);
        job.MarkDone(new byte[] { 0xFF }, "image/webp");

        var act = () => job.MarkProcessing(ProcessingStage.Decoding, 50);
        act.Should().Throw<InvalidOperationException>();
    }
}
