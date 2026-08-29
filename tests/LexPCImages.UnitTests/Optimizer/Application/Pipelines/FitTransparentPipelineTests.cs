using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Pipelines;
using LexPCImages.Modules.Optimizer.Application.Progress;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using NSubstitute;

namespace LexPCImages.UnitTests.Optimizer.Application.Pipelines;

public sealed class FitTransparentPipelineTests
{
    private static readonly SlotDefinition Slot = SlotDefinition.PcHomeSmall;
    private static readonly DecodedImage Source = new(800, 600, new byte[800 * 600 * 4]);
    private static readonly DecodedImage Output = new(Slot.Width, Slot.Height, new byte[Slot.Width * Slot.Height * 4]);

    private readonly IImageResizer _resizer = Substitute.For<IImageResizer>();
    private readonly IJobProgressNotifier _notifier = Substitute.For<IJobProgressNotifier>();
    private readonly Guid _jobId = Guid.NewGuid();

    public FitTransparentPipelineTests()
    {
        _resizer
            .ResizeAsync(
                Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<ResizeMode>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Output));
    }

    private FitTransparentPipeline CreateSut() => new(_resizer, _notifier);

    [Fact]
    public void Mode_is_FitTransparent()
    {
        CreateSut().Mode.Should().Be(SlotMode.FitTransparent);
    }

    [Fact]
    public async Task ExecuteAsync_fits_the_image_padding_with_transparency()
    {
        var context = new ImagePipelineContext(_jobId, Source, Slot);

        var result = await CreateSut().ExecuteAsync(context, CancellationToken.None);

        result.Should().Be(Output);
        await _resizer.Received(1).ResizeAsync(
            Source, Slot.Width, Slot.Height,
            ResizeMode.FitWithTransparentPadding, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_never_stretches_or_crops()
    {
        await CreateSut().ExecuteAsync(new ImagePipelineContext(_jobId, Source, Slot), CancellationToken.None);

        await _resizer.DidNotReceive().ResizeAsync(
            Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>(),
            ResizeMode.Stretch, Arg.Any<CancellationToken>());
        await _resizer.DidNotReceive().ResizeAsync(
            Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>(),
            ResizeMode.Cover, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_reports_a_single_resizing_stage()
    {
        await CreateSut().ExecuteAsync(new ImagePipelineContext(_jobId, Source, Slot), CancellationToken.None);

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
