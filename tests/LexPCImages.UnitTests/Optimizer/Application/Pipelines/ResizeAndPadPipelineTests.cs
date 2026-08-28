using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Pipelines;
using LexPCImages.Modules.Optimizer.Application.Progress;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using NSubstitute;

namespace LexPCImages.UnitTests.Optimizer.Application.Pipelines;

public sealed class ResizeAndPadPipelineTests
{
    private static readonly SlotDefinition Slot = SlotDefinition.PcMainSection;
    private static readonly DecodedImage Source = new(800, 450, new byte[800 * 450 * 4]);
    private static readonly DecodedImage Padded = new(Slot.Width, Slot.Height, new byte[Slot.Width * Slot.Height * 4]);

    private readonly IImagePadder _padder = Substitute.For<IImagePadder>();
    private readonly IJobProgressNotifier _notifier = Substitute.For<IJobProgressNotifier>();
    private readonly Guid _jobId = Guid.NewGuid();

    public ResizeAndPadPipelineTests()
    {
        _padder.Pad(Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PaddedImage(Padded, 0, 0));
    }

    private ResizeAndPadPipeline CreateSut() => new(_padder, _notifier);

    [Fact]
    public void Mode_is_ResizeAndPad()
    {
        CreateSut().Mode.Should().Be(SlotMode.ResizeAndPad);
    }

    [Fact]
    public async Task ExecuteAsync_pads_to_the_slot_dimensions()
    {
        var context = new ImagePipelineContext(_jobId, Source, Slot, RefinementOptions.Defaults);

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Should().Be(Padded);
        _padder.Received(1).Pad(Source, Slot.Width, Slot.Height);
    }

    [Fact]
    public async Task ExecuteAsync_reports_a_single_resizing_stage()
    {
        var context = new ImagePipelineContext(_jobId, Source, Slot, RefinementOptions.Defaults);

        await CreateSut().ExecuteAsync(context, CancellationToken.None);

        await _notifier.Received(1).OnStageStartedAsync(
            _jobId, ProcessingStage.Resizing, OptimizerProgress.ResizingAndPadding.Start, Arg.Any<CancellationToken>());
        await _notifier.Received(1).OnStageCompletedAsync(
            _jobId, ProcessingStage.Resizing, OptimizerProgress.ResizingAndPadding.End, Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().OnStageStartedAsync(
            _jobId, ProcessingStage.Inferring, Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().OnStageStartedAsync(
            _jobId, ProcessingStage.Cropping, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
