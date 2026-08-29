using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Pipelines;
using LexPCImages.Modules.Optimizer.Application.Progress;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using NSubstitute;

namespace LexPCImages.UnitTests.Optimizer.Application.Pipelines;

public sealed class CoverOrPadPipelineTests
{
    private static readonly SlotDefinition Slot = SlotDefinition.PcLastSection;
    private static readonly DecodedImage NearSquare = new(1000, 1000, new byte[1000 * 1000 * 4]);
    private static readonly DecodedImage Wide = new(1920, 1080, new byte[1920 * 1080 * 4]);
    private static readonly DecodedImage Output = new(Slot.Width, Slot.Height, new byte[Slot.Width * Slot.Height * 4]);

    private readonly IImageResizer _resizer = Substitute.For<IImageResizer>();
    private readonly IImagePadder _padder = Substitute.For<IImagePadder>();
    private readonly IJobProgressNotifier _notifier = Substitute.For<IJobProgressNotifier>();
    private readonly Guid _jobId = Guid.NewGuid();

    public CoverOrPadPipelineTests()
    {
        _resizer
            .ResizeAsync(
                Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<ResizeMode>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Output));
        _padder
            .Pad(Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PaddedImage(Output, 0, 0));
    }

    private CoverOrPadPipeline CreateSut() => new(_resizer, _padder, _notifier);

    private ImagePipelineContext ContextFor(DecodedImage source) =>
        new(_jobId, source, Slot);

    [Fact]
    public void Mode_is_CoverOrPad()
    {
        CreateSut().Mode.Should().Be(SlotMode.CoverOrPad);
    }

    [Fact]
    public async Task ExecuteAsync_crops_when_the_aspect_ratio_is_close_to_the_slot()
    {
        var result = await CreateSut().ExecuteAsync(ContextFor(NearSquare), CancellationToken.None);

        result.Should().Be(Output);
        await _resizer.Received(1).ResizeAsync(
            NearSquare, Slot.Width, Slot.Height, ResizeMode.Cover, Arg.Any<CancellationToken>());
        _padder.DidNotReceive().Pad(Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task ExecuteAsync_pads_when_cropping_would_eat_too_much_of_the_image()
    {
        var result = await CreateSut().ExecuteAsync(ContextFor(Wide), CancellationToken.None);

        result.Should().Be(Output);
        _padder.Received(1).Pad(Wide, Slot.Width, Slot.Height);
        await _resizer.DidNotReceive().ResizeAsync(
            Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<ResizeMode>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_honours_the_threshold_of_the_slot()
    {
        var permissiveSlot = new SlotDefinition(
            Slot.Id, Slot.Width, Slot.Height,
            mode: SlotMode.CoverOrPad,
            coverFit: new CoverFitOptions(minCoverage: 0.4));
        var context = new ImagePipelineContext(_jobId, Wide, permissiveSlot);

        await CreateSut().ExecuteAsync(context, CancellationToken.None);

        await _resizer.Received(1).ResizeAsync(
            Wide, Slot.Width, Slot.Height, ResizeMode.Cover, Arg.Any<CancellationToken>());
        _padder.DidNotReceive().Pad(Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_reports_a_single_resizing_stage_on_both_paths(bool cropping)
    {
        var context = ContextFor(cropping ? NearSquare : Wide);

        await CreateSut().ExecuteAsync(context, CancellationToken.None);

        await _notifier.Received(1).OnStageStartedAsync(
            _jobId, ProcessingStage.Resizing, OptimizerProgress.Resizing.Start, Arg.Any<CancellationToken>());
        await _notifier.Received(1).OnStageCompletedAsync(
            _jobId, ProcessingStage.Resizing, OptimizerProgress.Resizing.End, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_rejects_a_null_context()
    {
        var act = async () => await CreateSut().ExecuteAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
