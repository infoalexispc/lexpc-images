using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Ports;
using LexPCImages.Modules.Optimizer.Application.UseCases.EnqueueJob;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Shared.Common.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LexPCImages.UnitTests.Optimizer.Application;

public sealed class EnqueueJobHandlerTests
{
    private static readonly SlotId HomeSlot = SlotDefinition.PcHome.Id;

    /// <summary>PNG real: la validación comprueba la firma de los bytes, no solo el Content-Type.</summary>
    private static readonly byte[] ValidPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];
    private static readonly byte[] ValidJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static readonly byte[] ValidWebp =
        [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];

    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly ISlotRegistry _slots = Substitute.For<ISlotRegistry>();
    private readonly TimeProvider _time = Substitute.For<TimeProvider>();
    private readonly IJobQueueWriter _queue = Substitute.For<IJobQueueWriter>();
    private readonly DateTimeOffset _now = new(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);

    private ProcessJob? _persisted;

    public EnqueueJobHandlerTests()
    {
        _time.GetUtcNow().Returns(_now);
        _slots.FindById(HomeSlot).Returns(SlotDefinition.PcHome);
        _queue.TryEnqueue(Arg.Any<Guid>()).Returns(true);
        _jobs.When(repository => repository.AddAsync(Arg.Any<ProcessJob>(), Arg.Any<CancellationToken>()))
            .Do(call => _persisted = call.ArgAt<ProcessJob>(0));
    }

    private EnqueueJobHandler CreateSut() =>
        new(_jobs, _slots, _queue, NullLogger<EnqueueJobHandler>.Instance, _time);

    [Fact]
    public async Task HandleAsync_returns_SlotNotFound_when_slot_unknown()
    {
        _slots.FindById(Arg.Any<SlotId>()).Returns((SlotDefinition?)null);

        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidPng, "image/png"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("optimizer.slot_not_found");
    }

    [Fact]
    public async Task HandleAsync_returns_ImageEmpty_when_bytes_are_zero()
    {
        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(HomeSlot, [], "image/png"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("optimizer.image_empty");
    }

    [Fact]
    public async Task HandleAsync_returns_ImageTooLarge_when_exceeding_max()
    {
        var hugeImage = new byte[ProcessJob.MaxInputBytes + 1];

        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(HomeSlot, hugeImage, "image/png"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("optimizer.image_too_large");
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    public async Task HandleAsync_rejects_unsupported_content_types(string contentType)
    {
        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidPng, contentType), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("optimizer.image_format_not_supported");
    }

    [Fact]
    public async Task HandleAsync_rejects_a_file_whose_bytes_are_not_a_real_image()
    {
        var executableDisguisedAsPng = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 };

        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(HomeSlot, executableDisguisedAsPng, "image/png"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("optimizer.image_content_mismatch");
        await _jobs.DidNotReceive().AddAsync(Arg.Any<ProcessJob>(), Arg.Any<CancellationToken>());
    }

    public static TheoryData<string, byte[]> SupportedFormats() => new()
    {
        { "image/png", ValidPng },
        { "image/jpeg", ValidJpeg },
        { "image/webp", ValidWebp },
    };

    [Theory]
    [MemberData(nameof(SupportedFormats))]
    public async Task HandleAsync_accepts_supported_formats(string contentType, byte[] bytes)
    {
        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(HomeSlot, bytes, contentType), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(JobStatus.Queued);
        await _jobs.Received(1).AddAsync(
            Arg.Is<ProcessJob>(job => job.Slot.Id == HomeSlot && job.Status == JobStatus.Queued),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_stamps_the_job_with_the_injected_clock()
    {
        await CreateSut().HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidPng, "image/png"), CancellationToken.None);

        _persisted!.CreatedAt.Should().Be(_now);
    }

    [Fact]
    public async Task HandleAsync_writes_jobId_to_processing_queue()
    {
        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidPng, "image/png"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _queue.Received(1).TryEnqueue(result.Value!.JobId);
    }

    [Fact]
    public async Task HandleAsync_returns_unavailable_and_marks_job_as_error_when_queue_is_full()
    {
        _queue.TryEnqueue(Arg.Any<Guid>()).Returns(false);

        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidPng, "image/png"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("optimizer.processing_queue_full");
        _persisted.Should().NotBeNull();
        _persisted!.Status.Should().Be(JobStatus.Error);
        await _jobs.Received(1).UpdateAsync(_persisted, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_applies_overrides_to_the_persisted_job()
    {
        var overrides = new RefinementOverrides(
            SuppressShadow: false, RemoveDesk: false, ProtectLegs: true, CropMarginPct: 0.12);

        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidPng, "image/png", overrides), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _persisted!.EffectiveRefinement.SuppressShadow.Should().BeFalse();
        _persisted.EffectiveRefinement.RemoveDesk.Should().BeFalse();
        _persisted.EffectiveRefinement.ProtectLegs.Should().BeTrue();
        _persisted.EffectiveRefinement.CropMarginPct.Should().Be(0.12);
    }

    [Fact]
    public async Task HandleAsync_uses_slot_defaults_when_no_overrides_provided()
    {
        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidPng, "image/png"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _persisted!.EffectiveRefinement.Should().Be(SlotDefinition.PcHome.EffectiveRefinement);
    }

    [Fact]
    public async Task HandleAsync_partial_overrides_only_change_specified_fields()
    {
        var overrides = new RefinementOverrides(SuppressShadow: false);

        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidPng, "image/png", overrides), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _persisted!.EffectiveRefinement.SuppressShadow.Should().BeFalse();
        _persisted.EffectiveRefinement.RemoveDesk.Should().BeTrue();
        _persisted.EffectiveRefinement.ProtectLegs.Should().BeTrue();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(0.9)]
    public async Task HandleAsync_rejects_an_out_of_range_crop_margin_without_throwing(double margin)
    {
        var overrides = new RefinementOverrides(CropMarginPct: margin);

        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidPng, "image/png", overrides), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("optimizer.crop_margin_out_of_range");
        await _jobs.DidNotReceive().AddAsync(Arg.Any<ProcessJob>(), Arg.Any<CancellationToken>());
    }
}
