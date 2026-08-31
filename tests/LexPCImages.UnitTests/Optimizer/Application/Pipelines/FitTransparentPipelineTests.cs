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
    private static readonly DecodedImage Trimmed = new(600, 500, new byte[600 * 500 * 4]);
    private static readonly DecodedImage Output = new(Slot.Width, Slot.Height, new byte[Slot.Width * Slot.Height * 4]);

    private readonly IImageTrimmer _trimmer = Substitute.For<IImageTrimmer>();
    private readonly IImageResizer _resizer = Substitute.For<IImageResizer>();
    private readonly IJobProgressNotifier _notifier = Substitute.For<IJobProgressNotifier>();
    private readonly Guid _jobId = Guid.NewGuid();

    public FitTransparentPipelineTests()
    {
        _trimmer.TrimTransparentBorder(Arg.Any<DecodedImage>()).Returns(Trimmed);
        _resizer
            .ResizeAsync(
                Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<ResizeMode>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Output));
    }

    private FitTransparentPipeline CreateSut() => new(_trimmer, _resizer, _notifier);

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
            Trimmed, Slot.Width, Slot.Height,
            ResizeMode.FitWithTransparentPadding, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// El aire transparente del máster se descarta antes de escalar. Si se escalara el lienzo
    /// entero, los píxeles del slot se repartirían entre el producto y el vacío que lo rodea, y el
    /// producto saldría más pequeño —y por tanto menos nítido— de lo que cabe.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_trims_the_transparent_border_before_resizing()
    {
        await CreateSut().ExecuteAsync(new ImagePipelineContext(_jobId, Source, Slot), CancellationToken.None);

        _trimmer.Received(1).TrimTransparentBorder(Source);
        await _resizer.DidNotReceive().ResizeAsync(
            Source, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<ResizeMode>(), Arg.Any<CancellationToken>());
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
