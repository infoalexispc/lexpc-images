using FluentAssertions;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.UnitTests.Optimizer.Domain;

public sealed class ProcessJobTests
{
    private static readonly SlotDefinition Slot = SlotDefinition.PcHomeSmall;
    private static readonly byte[] AnyImage = [0x01, 0x02, 0x03];
    private static readonly byte[] AnyOutput = [0xFF, 0xFE];
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAt = CreatedAt.AddSeconds(30);

    private static ProcessJob NewJob() => ProcessJob.Create(Slot, AnyImage, "image/png", CreatedAt);

    private static ProcessJob ProcessingJob()
    {
        var job = NewJob();
        job.MarkProcessing(ProcessingStage.Decoding, 5, CreatedAt);
        return job;
    }

    [Fact]
    public void Create_starts_in_Queued_status_with_zero_progress()
    {
        var job = NewJob();

        job.Status.Should().Be(JobStatus.Queued);
        job.Progress.Should().Be(0);
        job.CurrentStage.Should().BeNull();
        job.StartedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
        job.IsTerminal.Should().BeFalse();
        job.CreatedAt.Should().Be(CreatedAt);
    }

    [Fact]
    public void MarkProcessing_transitions_to_Processing_with_stage_and_progress()
    {
        var job = NewJob();

        job.MarkProcessing(ProcessingStage.Decoding, 5, CreatedAt);

        job.Status.Should().Be(JobStatus.Processing);
        job.CurrentStage.Should().Be(ProcessingStage.Decoding);
        job.Progress.Should().Be(5);
        job.StartedAt.Should().Be(CreatedAt);
    }

    [Fact]
    public void MarkProcessing_uses_the_supplied_clock_instead_of_the_system_one()
    {
        var job = NewJob();
        var startedAt = CreatedAt.AddMinutes(5);

        job.MarkProcessing(ProcessingStage.Decoding, 5, startedAt);

        job.StartedAt.Should().Be(startedAt);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void MarkProcessing_rejects_invalid_progress(int percent)
    {
        var job = NewJob();

        var act = () => job.MarkProcessing(ProcessingStage.Decoding, percent, CreatedAt);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateProgress_only_works_in_Processing_status()
    {
        var job = NewJob();

        var act = () => job.UpdateProgress(ProcessingStage.Decoding, 10);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Cannot update progress*");
    }

    [Fact]
    public void UpdateProgress_advances_stage_and_progress()
    {
        var job = ProcessingJob();

        job.UpdateProgress(ProcessingStage.Resizing, 50);

        job.CurrentStage.Should().Be(ProcessingStage.Resizing);
        job.Progress.Should().Be(50);
    }

    [Fact]
    public void MarkDone_sets_status_Done_with_output()
    {
        var job = ProcessingJob();

        job.MarkDone(AnyOutput, "image/webp", CompletedAt);

        job.Status.Should().Be(JobStatus.Done);
        job.Progress.Should().Be(100);
        job.CurrentStage.Should().BeNull();
        job.OutputImage.Should().BeEquivalentTo(AnyOutput);
        job.OutputContentType.Should().Be("image/webp");
        job.CompletedAt.Should().Be(CompletedAt);
        job.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void MarkDone_is_idempotent_and_keeps_the_first_result()
    {
        var job = ProcessingJob();
        job.MarkDone(AnyOutput, "image/webp", CompletedAt);

        job.MarkDone([0x11, 0x22], "image/png", CompletedAt.AddMinutes(1));

        job.OutputImage.Should().BeEquivalentTo(AnyOutput);
        job.OutputContentType.Should().Be("image/webp");
        job.CompletedAt.Should().Be(CompletedAt);
    }

    [Fact]
    public void MarkDone_rejects_a_queued_job_that_never_ran_the_pipeline()
    {
        var job = NewJob();

        var act = () => job.MarkDone(AnyOutput, "image/webp", CompletedAt);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Invalid transition from Queued*");
    }

    [Fact]
    public void MarkDone_rejects_an_empty_output()
    {
        var job = ProcessingJob();

        var act = () => job.MarkDone([], "image/webp", CompletedAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkError_sets_status_Error_with_message()
    {
        var job = ProcessingJob();

        job.MarkError("model not loaded", CompletedAt);

        job.Status.Should().Be(JobStatus.Error);
        job.ErrorMessage.Should().Be("model not loaded");
        job.CurrentStage.Should().BeNull();
        job.CompletedAt.Should().Be(CompletedAt);
        job.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void MarkError_can_close_a_job_that_never_started()
    {
        var job = NewJob();

        job.MarkError("queue is full", CompletedAt);

        job.Status.Should().Be(JobStatus.Error);
    }

    [Fact]
    public void MarkError_after_Done_throws()
    {
        var job = ProcessingJob();
        job.MarkDone(AnyOutput, "image/webp", CompletedAt);

        var act = () => job.MarkError("late error", CompletedAt);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkProcessing_after_Done_throws()
    {
        var job = ProcessingJob();
        job.MarkDone(AnyOutput, "image/webp", CompletedAt);

        var act = () => job.MarkProcessing(ProcessingStage.Decoding, 50, CompletedAt);

        act.Should().Throw<InvalidOperationException>();
    }

}
