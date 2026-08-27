using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.UseCases.ProcessImage;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LexPCImages.UnitTests.Optimizer.Application;

public sealed class ProcessImageHandlerTests
{
    private static readonly SlotDefinition Slot = SlotDefinition.PcHome;
    private static readonly byte[] InputImage = new byte[] { 0xFF, 0xD8, 0xFF };
    private static readonly DecodedImage DecodedOriginal = new(400, 300, new byte[400 * 300 * 4]);
    private static readonly MaskResult FullMask = new(400, 300, Enumerable.Repeat(1f, 400 * 300).ToArray());
    private static readonly CroppedImage CroppedPassThrough = new(DecodedOriginal, FullMask);
    private static readonly DecodedImage MaskedPassThrough = new(DecodedOriginal.Width, DecodedOriginal.Height, new byte[DecodedOriginal.Width * DecodedOriginal.Height * 4]);
    private static readonly DecodedImage Resized = new(Slot.Width, Slot.Height, new byte[Slot.Width * Slot.Height * 4]);
    private static readonly byte[] EncodedWebp = new byte[] { 0x52, 0x49, 0x46, 0x46, 0xDE, 0xAD, 0xBE, 0xEF };

    [Fact]
    public async Task HandleAsync_returns_success_when_pipeline_completes()
    {
        var decoder = Substitute.For<IImageDecoder>();
        var remover = Substitute.For<IBackgroundRemovalService>();
        var shadowSuppressor = Substitute.For<IShadowSuppressor>();
        var deskRefiner = Substitute.For<IDeskMaskRefiner>();
        var legProtector = Substitute.For<ILegProtector>();
        var tightCropper = Substitute.For<ITightCropper>();
        var resizer = Substitute.For<IImageResizer>();
        var encoder = Substitute.For<IImageEncoder>();
        var notifier = Substitute.For<IJobProgressNotifier>();

        decoder.DecodeAsync(InputImage, Arg.Any<CancellationToken>()).Returns(DecodedOriginal);
        remover.RemoveBackgroundAsync(DecodedOriginal, Arg.Any<CancellationToken>()).Returns(FullMask);
        shadowSuppressor.Suppress(DecodedOriginal, FullMask).Returns(FullMask);
        deskRefiner.RemoveDesk(FullMask).Returns(FullMask);
        legProtector.Protect(DecodedOriginal, FullMask).Returns(FullMask);
        tightCropper.Crop(DecodedOriginal, FullMask, Arg.Any<double>()).Returns(CroppedPassThrough);
        resizer.ResizeAsync(Arg.Any<DecodedImage>(), Slot.Width, Slot.Height, ResizeMode.Stretch, Arg.Any<CancellationToken>())
            .Returns(Resized);
        encoder.EncodeWebPAsync(Resized, Arg.Any<CancellationToken>()).Returns(EncodedWebp);

        var handler = BuildHandler(decoder, remover, shadowSuppressor, deskRefiner, legProtector, tightCropper, resizer, encoder, notifier);
        var job = ProcessJob.Create(Slot, InputImage, "image/jpeg", DateTimeOffset.UtcNow);

        var result = await handler.HandleAsync(job, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(EncodedWebp);
        await decoder.Received(1).DecodeAsync(InputImage, Arg.Any<CancellationToken>());
        await remover.Received(1).RemoveBackgroundAsync(DecodedOriginal, Arg.Any<CancellationToken>());
        shadowSuppressor.Received(1).Suppress(DecodedOriginal, FullMask);
        deskRefiner.Received(1).RemoveDesk(FullMask);
        legProtector.Received(1).Protect(DecodedOriginal, FullMask);
        tightCropper.Received(1).Crop(DecodedOriginal, FullMask, Arg.Any<double>());
        await resizer.Received(1).ResizeAsync(Arg.Any<DecodedImage>(), Slot.Width, Slot.Height, ResizeMode.Stretch, Arg.Any<CancellationToken>());
        await encoder.Received(1).EncodeWebPAsync(Resized, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_emits_progress_notifications_in_order()
    {
        var decoder = Substitute.For<IImageDecoder>();
        var remover = Substitute.For<IBackgroundRemovalService>();
        var shadowSuppressor = Substitute.For<IShadowSuppressor>();
        var deskRefiner = Substitute.For<IDeskMaskRefiner>();
        var legProtector = Substitute.For<ILegProtector>();
        var tightCropper = Substitute.For<ITightCropper>();
        var resizer = Substitute.For<IImageResizer>();
        var encoder = Substitute.For<IImageEncoder>();
        var notifier = Substitute.For<IJobProgressNotifier>();

        decoder.DecodeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(DecodedOriginal);
        remover.RemoveBackgroundAsync(Arg.Any<DecodedImage>(), Arg.Any<CancellationToken>()).Returns(FullMask);
        shadowSuppressor.Suppress(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>()).Returns(FullMask);
        deskRefiner.RemoveDesk(Arg.Any<MaskResult>()).Returns(FullMask);
        legProtector.Protect(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>()).Returns(FullMask);
        tightCropper.Crop(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>(), Arg.Any<double>()).Returns(CroppedPassThrough);
        resizer.ResizeAsync(Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<ResizeMode>(), Arg.Any<CancellationToken>()).Returns(Resized);
        encoder.EncodeWebPAsync(Arg.Any<DecodedImage>(), Arg.Any<CancellationToken>()).Returns(EncodedWebp);

        var handler = BuildHandler(decoder, remover, shadowSuppressor, deskRefiner, legProtector, tightCropper, resizer, encoder, notifier);
        var job = ProcessJob.Create(Slot, InputImage, "image/jpeg", DateTimeOffset.UtcNow);

        await handler.HandleAsync(job, CancellationToken.None);

        Received.InOrder(async () =>
        {
            notifier.OnStageStarted(job.Id, ProcessingStage.Decoding, 10);
            notifier.OnStageCompleted(job.Id, ProcessingStage.Decoding, 20);
            notifier.OnStageStarted(job.Id, ProcessingStage.Inferring, 25);
            notifier.OnStageCompleted(job.Id, ProcessingStage.Inferring, 55);
            notifier.OnStageStarted(job.Id, ProcessingStage.LegProtecting, 55);
            notifier.OnStageCompleted(job.Id, ProcessingStage.LegProtecting, 62);
            notifier.OnStageStarted(job.Id, ProcessingStage.DeskRemoving, 62);
            notifier.OnStageCompleted(job.Id, ProcessingStage.DeskRemoving, 70);
            notifier.OnStageStarted(job.Id, ProcessingStage.ShadowSuppressing, 70);
            notifier.OnStageCompleted(job.Id, ProcessingStage.ShadowSuppressing, 76);
            notifier.OnStageStarted(job.Id, ProcessingStage.Cropping, 76);
            notifier.OnStageCompleted(job.Id, ProcessingStage.Cropping, 82);
            notifier.OnStageStarted(job.Id, ProcessingStage.Resizing, 82);
            notifier.OnStageCompleted(job.Id, ProcessingStage.Resizing, 94);
            notifier.OnStageStarted(job.Id, ProcessingStage.Encoding, 96);
            notifier.OnStageCompleted(job.Id, ProcessingStage.Encoding, 100);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task HandleAsync_throws_when_image_too_small()
    {
        var decoder = Substitute.For<IImageDecoder>();
        decoder.DecodeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new DecodedImage(50, 50, new byte[50 * 50 * 4]));

        var handler = BuildHandler(
            decoder,
            Substitute.For<IBackgroundRemovalService>(),
            Substitute.For<IShadowSuppressor>(),
            Substitute.For<IDeskMaskRefiner>(),
            Substitute.For<ILegProtector>(),
            Substitute.For<ITightCropper>(),
            Substitute.For<IImageResizer>(),
            Substitute.For<IImageEncoder>(),
            Substitute.For<IJobProgressNotifier>());

        var job = ProcessJob.Create(Slot, InputImage, "image/jpeg", DateTimeOffset.UtcNow);

        var act = () => handler.HandleAsync(job, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*too small*");
    }

    [Fact]
    public async Task HandleAsync_throws_when_image_too_large()
    {
        var decoder = Substitute.For<IImageDecoder>();
        decoder.DecodeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new DecodedImage(9000, 9000, new byte[9000 * 9000 * 4]));

        var handler = BuildHandler(
            decoder,
            Substitute.For<IBackgroundRemovalService>(),
            Substitute.For<IShadowSuppressor>(),
            Substitute.For<IDeskMaskRefiner>(),
            Substitute.For<ILegProtector>(),
            Substitute.For<ITightCropper>(),
            Substitute.For<IImageResizer>(),
            Substitute.For<IImageEncoder>(),
            Substitute.For<IJobProgressNotifier>());

        var job = ProcessJob.Create(Slot, InputImage, "image/jpeg", DateTimeOffset.UtcNow);

        var act = () => handler.HandleAsync(job, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*too large*");
    }

    [Fact]
    public async Task HandleAsync_applies_cropped_mask_to_resized_image()
    {
        var decoder = Substitute.For<IImageDecoder>();
        var remover = Substitute.For<IBackgroundRemovalService>();
        var shadowSuppressor = Substitute.For<IShadowSuppressor>();
        var deskRefiner = Substitute.For<IDeskMaskRefiner>();
        var legProtector = Substitute.For<ILegProtector>();
        var tightCropper = Substitute.For<ITightCropper>();
        var resizer = Substitute.For<IImageResizer>();
        var encoder = Substitute.For<IImageEncoder>();
        var notifier = Substitute.For<IJobProgressNotifier>();

        var croppedMaskValues = Enumerable.Repeat(0.5f, 200 * 200).ToArray();
        var croppedImage = new DecodedImage(200, 200, new byte[200 * 200 * 4]);
        var croppedMask = new MaskResult(200, 200, croppedMaskValues);
        var croppedResult = new CroppedImage(croppedImage, croppedMask);

        decoder.DecodeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(DecodedOriginal);
        remover.RemoveBackgroundAsync(Arg.Any<DecodedImage>(), Arg.Any<CancellationToken>()).Returns(FullMask);
        shadowSuppressor.Suppress(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>()).Returns(FullMask);
        deskRefiner.RemoveDesk(Arg.Any<MaskResult>()).Returns(FullMask);
        legProtector.Protect(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>()).Returns(FullMask);
        tightCropper.Crop(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>(), Arg.Any<double>()).Returns(croppedResult);
        resizer.ResizeAsync(Arg.Any<DecodedImage>(), Slot.Width, Slot.Height, ResizeMode.Stretch, Arg.Any<CancellationToken>())
            .Returns(Resized);
        encoder.EncodeWebPAsync(Arg.Any<DecodedImage>(), Arg.Any<CancellationToken>()).Returns(EncodedWebp);

        var handler = BuildHandler(decoder, remover, shadowSuppressor, deskRefiner, legProtector, tightCropper, resizer, encoder, notifier);
        var job = ProcessJob.Create(Slot, InputImage, "image/jpeg", DateTimeOffset.UtcNow);

        await handler.HandleAsync(job, CancellationToken.None);

        await resizer.Received(1).ResizeAsync(
            Arg.Is<DecodedImage>(d => d.Width == 200 && d.Height == 200 && d.Rgba.Length == 200 * 200 * 4),
            Slot.Width,
            Slot.Height,
            ResizeMode.Stretch,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_skips_suppressed_stages_when_refinement_disables_them()
    {
        var disabledSlot = new SlotDefinition(
            SlotDefinition.PcHome.Id,
            SlotDefinition.PcHome.Width,
            SlotDefinition.PcHome.Height,
            Refinement: new RefinementOptions(SuppressShadow: false, RemoveDesk: false, ProtectLegs: false));

        var decoder = Substitute.For<IImageDecoder>();
        var remover = Substitute.For<IBackgroundRemovalService>();
        var shadowSuppressor = Substitute.For<IShadowSuppressor>();
        var deskRefiner = Substitute.For<IDeskMaskRefiner>();
        var legProtector = Substitute.For<ILegProtector>();
        var tightCropper = Substitute.For<ITightCropper>();
        var resizer = Substitute.For<IImageResizer>();
        var encoder = Substitute.For<IImageEncoder>();
        var notifier = Substitute.For<IJobProgressNotifier>();

        decoder.DecodeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(DecodedOriginal);
        remover.RemoveBackgroundAsync(Arg.Any<DecodedImage>(), Arg.Any<CancellationToken>()).Returns(FullMask);
        tightCropper.Crop(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>(), Arg.Any<double>()).Returns(CroppedPassThrough);
        resizer.ResizeAsync(Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<ResizeMode>(), Arg.Any<CancellationToken>()).Returns(Resized);
        encoder.EncodeWebPAsync(Arg.Any<DecodedImage>(), Arg.Any<CancellationToken>()).Returns(EncodedWebp);

        var handler = BuildHandler(decoder, remover, shadowSuppressor, deskRefiner, legProtector, tightCropper, resizer, encoder, notifier);
        var job = ProcessJob.Create(disabledSlot, InputImage, "image/jpeg", DateTimeOffset.UtcNow);

        await handler.HandleAsync(job, CancellationToken.None);

        shadowSuppressor.DidNotReceive().Suppress(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>());
        deskRefiner.DidNotReceive().RemoveDesk(Arg.Any<MaskResult>());
        legProtector.DidNotReceive().Protect(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>());
        notifier.DidNotReceive().OnStageStarted(job.Id, ProcessingStage.ShadowSuppressing, Arg.Any<int>());
        notifier.DidNotReceive().OnStageStarted(job.Id, ProcessingStage.DeskRemoving, Arg.Any<int>());
        notifier.DidNotReceive().OnStageStarted(job.Id, ProcessingStage.LegProtecting, Arg.Any<int>());
    }

    [Fact]
    public async Task HandleAsync_passes_crop_margin_from_slot_to_cropper()
    {
        var customSlot = new SlotDefinition(
            SlotDefinition.PcHome.Id,
            SlotDefinition.PcHome.Width,
            SlotDefinition.PcHome.Height,
            Refinement: new RefinementOptions(
                SuppressShadow: false,
                RemoveDesk: false,
                ProtectLegs: false,
                CropMarginPct: 0.12));

        var decoder = Substitute.For<IImageDecoder>();
        var remover = Substitute.For<IBackgroundRemovalService>();
        var tightCropper = Substitute.For<ITightCropper>();
        var resizer = Substitute.For<IImageResizer>();
        var encoder = Substitute.For<IImageEncoder>();

        decoder.DecodeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(DecodedOriginal);
        remover.RemoveBackgroundAsync(Arg.Any<DecodedImage>(), Arg.Any<CancellationToken>()).Returns(FullMask);
        tightCropper.Crop(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>(), Arg.Any<double>()).Returns(CroppedPassThrough);
        resizer.ResizeAsync(Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<ResizeMode>(), Arg.Any<CancellationToken>()).Returns(Resized);
        encoder.EncodeWebPAsync(Arg.Any<DecodedImage>(), Arg.Any<CancellationToken>()).Returns(EncodedWebp);

        var handler = BuildHandler(
            decoder, remover,
            Substitute.For<IShadowSuppressor>(),
            Substitute.For<IDeskMaskRefiner>(),
            Substitute.For<ILegProtector>(),
            tightCropper, resizer, encoder,
            Substitute.For<IJobProgressNotifier>());
        var job = ProcessJob.Create(customSlot, InputImage, "image/jpeg", DateTimeOffset.UtcNow);

        await handler.HandleAsync(job, CancellationToken.None);

        tightCropper.Received(1).Crop(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>(), 0.12);
    }

    [Fact]
    public async Task HandleAsync_uses_default_refinement_when_slot_has_null()
    {
        var slotWithNull = new SlotDefinition(
            SlotDefinition.PcHome.Id,
            SlotDefinition.PcHome.Width,
            SlotDefinition.PcHome.Height,
            Refinement: null);

        var decoder = Substitute.For<IImageDecoder>();
        var remover = Substitute.For<IBackgroundRemovalService>();
        var shadowSuppressor = Substitute.For<IShadowSuppressor>();
        var deskRefiner = Substitute.For<IDeskMaskRefiner>();
        var legProtector = Substitute.For<ILegProtector>();
        var tightCropper = Substitute.For<ITightCropper>();
        var resizer = Substitute.For<IImageResizer>();
        var encoder = Substitute.For<IImageEncoder>();

        decoder.DecodeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(DecodedOriginal);
        remover.RemoveBackgroundAsync(Arg.Any<DecodedImage>(), Arg.Any<CancellationToken>()).Returns(FullMask);
        shadowSuppressor.Suppress(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>()).Returns(FullMask);
        deskRefiner.RemoveDesk(Arg.Any<MaskResult>()).Returns(FullMask);
        legProtector.Protect(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>()).Returns(FullMask);
        tightCropper.Crop(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>(), Arg.Any<double>()).Returns(CroppedPassThrough);
        resizer.ResizeAsync(Arg.Any<DecodedImage>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<ResizeMode>(), Arg.Any<CancellationToken>()).Returns(Resized);
        encoder.EncodeWebPAsync(Arg.Any<DecodedImage>(), Arg.Any<CancellationToken>()).Returns(EncodedWebp);

        var handler = BuildHandler(decoder, remover, shadowSuppressor, deskRefiner, legProtector, tightCropper, resizer, encoder, Substitute.For<IJobProgressNotifier>());
        var job = ProcessJob.Create(slotWithNull, InputImage, "image/jpeg", DateTimeOffset.UtcNow);

        await handler.HandleAsync(job, CancellationToken.None);

        shadowSuppressor.Received(1).Suppress(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>());
        deskRefiner.Received(1).RemoveDesk(Arg.Any<MaskResult>());
        legProtector.Received(1).Protect(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>());
        tightCropper.Received(1).Crop(Arg.Any<DecodedImage>(), Arg.Any<MaskResult>(), RefinementOptions.Defaults.CropMarginPct);
    }

    private static ProcessImageHandler BuildHandler(
        IImageDecoder decoder,
        IBackgroundRemovalService remover,
        IShadowSuppressor shadowSuppressor,
        IDeskMaskRefiner deskRefiner,
        ILegProtector legProtector,
        ITightCropper tightCropper,
        IImageResizer resizer,
        IImageEncoder encoder,
        IJobProgressNotifier notifier) =>
        new(decoder, remover, shadowSuppressor, deskRefiner, legProtector, tightCropper, resizer, encoder, notifier, NullLogger<ProcessImageHandler>.Instance);
}
