using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Pipelines;
using LexPCImages.Modules.Optimizer.Application.Progress;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using NSubstitute;

namespace LexPCImages.UnitTests.Optimizer.Application.Pipelines;

public sealed class BackgroundRemovalPipelineTests
{
    private static readonly SlotDefinition Slot = SlotDefinition.PcHome;
    private static readonly DecodedImage Source = new(400, 300, new byte[400 * 300 * 4]);
    private static readonly MaskResult FullMask = new(400, 300, [.. Enumerable.Repeat(1f, 400 * 300)]);
    private static readonly DecodedImage Resized = new(Slot.Width, Slot.Height, new byte[Slot.Width * Slot.Height * 4]);

    private readonly IBackgroundRemovalService _remover = Substitute.For<IBackgroundRemovalService>();
    private readonly ILegProtector _legProtector = Substitute.For<ILegProtector>();
    private readonly IDeskMaskRefiner _deskRefiner = Substitute.For<IDeskMaskRefiner>();
    private readonly IShadowSuppressor _shadowSuppressor = Substitute.For<IShadowSuppressor>();
    private readonly ITightCropper _cropper = Substitute.For<ITightCropper>();
    private readonly IImageResizer _resizer = Substitute.For<IImageResizer>();
    private readonly IJobProgressNotifier _notifier = Substitute.For<IJobProgressNotifier>();
    private readonly Guid _jobId = Guid.NewGuid();

    public BackgroundRemovalPipelineTests()
    {
        _remover.RemoveBackgroundAsync(Arg.Any<DecodedImage>(), Arg.Any<CancellationToken>()).Returns(FullMask);
        _legProtector.Protect(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>()).Returns(FullMask);
        _deskRefiner.RemoveDesk(Arg.Any<MaskResult>()).Returns(FullMask);
        _shadowSuppressor.Suppress(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>()).Returns(FullMask);
        _cropper.Crop(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>(), Arg.Any<double>())
            .Returns(new CroppedImage(Source, FullMask));
        _resizer.ResizeAsync(
                Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<ResizeMode>(), Arg.Any<CancellationToken>())
            .Returns(Resized);
    }

    private BackgroundRemovalPipeline CreateSut() => new(
        _remover, _legProtector, _deskRefiner, _shadowSuppressor, _cropper, _resizer, _notifier);

    private ImagePipelineContext Context(RefinementOptions? refinement = null) =>
        new(_jobId, Source, Slot, refinement ?? RefinementOptions.Defaults);

    [Fact]
    public void Mode_is_BackgroundRemoval()
    {
        CreateSut().Mode.Should().Be(SlotMode.BackgroundRemoval);
    }

    [Fact]
    public async Task ExecuteAsync_runs_every_stage_with_the_default_refinement()
    {
        var result = await CreateSut().ExecuteAsync(Context(), CancellationToken.None);

        result.Should().Be(Resized);
        await _remover.Received(1).RemoveBackgroundAsync(Source, Arg.Any<CancellationToken>());
        _legProtector.Received(1).Protect(Source, FullMask);
        _deskRefiner.Received(1).RemoveDesk(FullMask);
        _shadowSuppressor.Received(1).Suppress(Source, FullMask);
        _cropper.Received(1).Crop(Source, FullMask, RefinementOptions.Defaults.CropMarginPct);
        await _resizer.Received(1).ResizeAsync(
            Arg.Any<DecodedImage>(), Slot.Width, Slot.Height, ResizeMode.Stretch, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_skips_the_refinements_that_are_disabled()
    {
        var refinement = new RefinementOptions(
            suppressShadow: false, removeDesk: false, protectLegs: false);

        await CreateSut().ExecuteAsync(Context(refinement), CancellationToken.None);

        _legProtector.DidNotReceive().Protect(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>());
        _deskRefiner.DidNotReceive().RemoveDesk(Arg.Any<MaskResult>());
        _shadowSuppressor.DidNotReceive().Suppress(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>());
        await _notifier.DidNotReceive().OnStageStartedAsync(
            _jobId, ProcessingStage.LegProtecting, Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().OnStageStartedAsync(
            _jobId, ProcessingStage.DeskRemoving, Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().OnStageStartedAsync(
            _jobId, ProcessingStage.ShadowSuppressing, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_passes_the_crop_margin_from_the_refinement()
    {
        var refinement = new RefinementOptions(cropMarginPct: 0.12);

        await CreateSut().ExecuteAsync(Context(refinement), CancellationToken.None);

        _cropper.Received(1).Crop(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>(), 0.12);
    }

    [Fact]
    public async Task ExecuteAsync_resizes_the_cropped_image_not_the_original()
    {
        var cropped = new DecodedImage(200, 200, new byte[200 * 200 * 4]);
        var croppedMask = new MaskResult(200, 200, [.. Enumerable.Repeat(0.5f, 200 * 200)]);
        _cropper.Crop(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>(), Arg.Any<double>())
            .Returns(new CroppedImage(cropped, croppedMask));

        await CreateSut().ExecuteAsync(Context(), CancellationToken.None);

        await _resizer.Received(1).ResizeAsync(
            Arg.Is<DecodedImage>(image => image.Width == 200 && image.Height == 200),
            Slot.Width,
            Slot.Height,
            ResizeMode.Stretch,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_reports_the_stages_in_order()
    {
        await CreateSut().ExecuteAsync(Context(), CancellationToken.None);

        Received.InOrder(async () =>
        {
            await _notifier.OnStageStartedAsync(
                _jobId, ProcessingStage.Inferring, OptimizerProgress.Inferring.Start, Arg.Any<CancellationToken>());
            await _notifier.OnStageCompletedAsync(
                _jobId, ProcessingStage.Inferring, OptimizerProgress.Inferring.End, Arg.Any<CancellationToken>());
            await _notifier.OnStageStartedAsync(
                _jobId, ProcessingStage.LegProtecting, OptimizerProgress.LegProtecting.Start, Arg.Any<CancellationToken>());
            await _notifier.OnStageCompletedAsync(
                _jobId, ProcessingStage.LegProtecting, OptimizerProgress.LegProtecting.End, Arg.Any<CancellationToken>());
            await _notifier.OnStageStartedAsync(
                _jobId, ProcessingStage.DeskRemoving, OptimizerProgress.DeskRemoving.Start, Arg.Any<CancellationToken>());
            await _notifier.OnStageCompletedAsync(
                _jobId, ProcessingStage.DeskRemoving, OptimizerProgress.DeskRemoving.End, Arg.Any<CancellationToken>());
            await _notifier.OnStageStartedAsync(
                _jobId, ProcessingStage.ShadowSuppressing, OptimizerProgress.ShadowSuppressing.Start, Arg.Any<CancellationToken>());
            await _notifier.OnStageCompletedAsync(
                _jobId, ProcessingStage.ShadowSuppressing, OptimizerProgress.ShadowSuppressing.End, Arg.Any<CancellationToken>());
            await _notifier.OnStageStartedAsync(
                _jobId, ProcessingStage.Cropping, OptimizerProgress.Cropping.Start, Arg.Any<CancellationToken>());
            await _notifier.OnStageCompletedAsync(
                _jobId, ProcessingStage.Cropping, OptimizerProgress.Cropping.End, Arg.Any<CancellationToken>());
            await _notifier.OnStageStartedAsync(
                _jobId, ProcessingStage.Resizing, OptimizerProgress.Resizing.Start, Arg.Any<CancellationToken>());
            await _notifier.OnStageCompletedAsync(
                _jobId, ProcessingStage.Resizing, OptimizerProgress.Resizing.End, Arg.Any<CancellationToken>());
            await Task.CompletedTask;
        });
    }
}
