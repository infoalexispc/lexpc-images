using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Pipelines;
using LexPCImages.Modules.Optimizer.Application.Progress;
using LexPCImages.Modules.Optimizer.Application.UseCases.ProcessImage;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LexPCImages.UnitTests.Optimizer.Application;

/// <summary>
/// El orquestador solo decodifica, elige la estrategia del slot y codifica. Las transformaciones
/// se prueban en los tests de cada <see cref="IImageProcessingPipeline"/>.
/// </summary>
public sealed class ProcessImageHandlerTests
{
    private static readonly SlotDefinition Slot = SlotDefinition.PcHome;
    private static readonly byte[] InputImage = [0xFF, 0xD8, 0xFF];
    private static readonly DecodedImage Decoded = new(400, 300, new byte[400 * 300 * 4]);
    private static readonly DecodedImage Processed = new(Slot.Width, Slot.Height, new byte[Slot.Width * Slot.Height * 4]);
    private static readonly EncodedImage Encoded = new([0x52, 0x49, 0x46, 0x46], "image/webp");

    private readonly IImageDecoder _decoder = Substitute.For<IImageDecoder>();
    private readonly IImageEncoder _encoder = Substitute.For<IImageEncoder>();
    private readonly IJobProgressNotifier _notifier = Substitute.For<IJobProgressNotifier>();

    public ProcessImageHandlerTests()
    {
        _decoder.DecodeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(Decoded);
        _encoder.EncodeAsync(Arg.Any<DecodedImage>(), Arg.Any<CancellationToken>()).Returns(Encoded);
    }

    private ProcessImageHandler CreateSut(params IImageProcessingPipeline[] pipelines) =>
        new(_decoder, _encoder, _notifier, pipelines, NullLogger<ProcessImageHandler>.Instance);

    private static IImageProcessingPipeline FakePipeline(SlotMode mode, DecodedImage output)
    {
        var pipeline = Substitute.For<IImageProcessingPipeline>();
        pipeline.Mode.Returns(mode);
        pipeline.ExecuteAsync(Arg.Any<ImagePipelineContext>(), Arg.Any<CancellationToken>()).Returns(output);
        return pipeline;
    }

    private static ProcessJob NewJob(SlotDefinition? slot = null) =>
        ProcessJob.Create(slot ?? Slot, InputImage, "image/jpeg", DateTimeOffset.UtcNow);

    [Fact]
    public async Task HandleAsync_decodes_runs_the_pipeline_and_encodes()
    {
        var pipeline = FakePipeline(SlotMode.BackgroundRemoval, Processed);
        var job = NewJob();

        var result = await CreateSut(pipeline).HandleAsync(job, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().Equal(Encoded.Content);
        result.Value.ContentType.Should().Be("image/webp");
        await _decoder.Received(1).DecodeAsync(InputImage, Arg.Any<CancellationToken>());
        await pipeline.Received(1).ExecuteAsync(Arg.Any<ImagePipelineContext>(), Arg.Any<CancellationToken>());
        await _encoder.Received(1).EncodeAsync(Processed, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_selects_the_pipeline_that_matches_the_slot_mode()
    {
        var backgroundRemoval = FakePipeline(SlotMode.BackgroundRemoval, Processed);
        var resizeAndPad = FakePipeline(SlotMode.ResizeAndPad, Processed);
        var job = NewJob(SlotDefinition.PcMainSection);

        await CreateSut(backgroundRemoval, resizeAndPad).HandleAsync(job, CancellationToken.None);

        await resizeAndPad.Received(1).ExecuteAsync(Arg.Any<ImagePipelineContext>(), Arg.Any<CancellationToken>());
        await backgroundRemoval.DidNotReceive()
            .ExecuteAsync(Arg.Any<ImagePipelineContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_passes_the_effective_refinement_to_the_pipeline()
    {
        var pipeline = FakePipeline(SlotMode.BackgroundRemoval, Processed);
        var refinement = new RefinementOptions(suppressShadow: false, cropMarginPct: 0.3);
        var job = ProcessJob.Create(Slot, InputImage, "image/jpeg", DateTimeOffset.UtcNow, refinement);

        await CreateSut(pipeline).HandleAsync(job, CancellationToken.None);

        await pipeline.Received(1).ExecuteAsync(
            Arg.Is<ImagePipelineContext>(context =>
                context.JobId == job.Id
                && context.Slot == Slot
                && context.Source == Decoded
                && context.Refinement == refinement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_reports_decoding_and_encoding_around_the_pipeline()
    {
        var pipeline = FakePipeline(SlotMode.BackgroundRemoval, Processed);
        var job = NewJob();

        await CreateSut(pipeline).HandleAsync(job, CancellationToken.None);

        Received.InOrder(async () =>
        {
            await _notifier.OnStageStartedAsync(
                job.Id, ProcessingStage.Decoding, OptimizerProgress.Decoding.Start, Arg.Any<CancellationToken>());
            await _notifier.OnStageCompletedAsync(
                job.Id, ProcessingStage.Decoding, OptimizerProgress.Decoding.End, Arg.Any<CancellationToken>());
            await _notifier.OnStageStartedAsync(
                job.Id, ProcessingStage.Encoding, OptimizerProgress.Encoding.Start, Arg.Any<CancellationToken>());
            await _notifier.OnStageCompletedAsync(
                job.Id, ProcessingStage.Encoding, OptimizerProgress.Encoding.End, Arg.Any<CancellationToken>());
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task HandleAsync_returns_an_error_when_no_pipeline_handles_the_slot_mode()
    {
        var job = NewJob(SlotDefinition.PcMainSection);

        var result = await CreateSut(FakePipeline(SlotMode.BackgroundRemoval, Processed))
            .HandleAsync(job, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("optimizer.pipeline_not_available");
        await _decoder.DidNotReceive().DecodeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_rejects_two_pipelines_claiming_the_same_slot_mode()
    {
        var act = () => CreateSut(
            FakePipeline(SlotMode.BackgroundRemoval, Processed),
            FakePipeline(SlotMode.BackgroundRemoval, Processed));

        act.Should().Throw<InvalidOperationException>().WithMessage("*More than one pipeline*");
    }

    [Fact]
    public async Task HandleAsync_returns_validation_error_when_image_too_small()
    {
        _decoder.DecodeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new DecodedImage(50, 50, new byte[50 * 50 * 4]));
        var pipeline = FakePipeline(SlotMode.BackgroundRemoval, Processed);

        var result = await CreateSut(pipeline).HandleAsync(NewJob(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("optimizer.image_too_small");
        await pipeline.DidNotReceive().ExecuteAsync(Arg.Any<ImagePipelineContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_returns_validation_error_when_image_too_large()
    {
        _decoder.DecodeAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new DecodedImage(9000, 9000, []));
        var pipeline = FakePipeline(SlotMode.BackgroundRemoval, Processed);

        var result = await CreateSut(pipeline).HandleAsync(NewJob(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("optimizer.image_dimensions_too_large");
        await pipeline.DidNotReceive().ExecuteAsync(Arg.Any<ImagePipelineContext>(), Arg.Any<CancellationToken>());
    }
}
