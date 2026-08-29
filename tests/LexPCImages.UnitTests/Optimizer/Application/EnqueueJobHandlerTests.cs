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
    private static readonly SlotId SingleSlot = SlotDefinition.PcMainSection.Id;
    private static readonly SlotId BundleSlot = SlotBundle.PcHome.Id;

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

    private readonly List<ProcessJob> _persisted = [];

    public EnqueueJobHandlerTests()
    {
        _time.GetUtcNow().Returns(_now);
        _slots.Resolve(Arg.Any<SlotId>()).Returns([]);
        _slots.Resolve(SingleSlot).Returns([SlotDefinition.PcMainSection]);
        _slots.Resolve(BundleSlot).Returns([SlotDefinition.PcHomeSmall, SlotDefinition.PcHomeWide]);
        _queue.TryEnqueue(Arg.Any<Guid>()).Returns(true);
        _jobs.When(repository => repository.AddAsync(Arg.Any<ProcessJob>(), Arg.Any<CancellationToken>()))
            .Do(call => _persisted.Add(call.ArgAt<ProcessJob>(0)));
    }

    private EnqueueJobHandler CreateSut() =>
        new(_jobs, _slots, _queue, NullLogger<EnqueueJobHandler>.Instance, _time);

    [Fact]
    public async Task HandleAsync_returns_SlotNotFound_when_slot_unknown()
    {
        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(SlotId.Parse("no-existe"), ValidPng, "image/png"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("optimizer.slot_not_found");
    }

    [Fact]
    public async Task HandleAsync_returns_ImageEmpty_when_bytes_are_zero()
    {
        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(SingleSlot, [], "image/png"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("optimizer.image_empty");
    }

    [Fact]
    public async Task HandleAsync_returns_ImageTooLarge_when_exceeding_max()
    {
        var hugeImage = new byte[ProcessJob.MaxInputBytes + 1];

        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(SingleSlot, hugeImage, "image/png"), CancellationToken.None);

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
            new EnqueueJobCommand(SingleSlot, ValidPng, contentType), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("optimizer.image_format_not_supported");
    }

    [Fact]
    public async Task HandleAsync_rejects_a_file_whose_bytes_are_not_a_real_image()
    {
        var executableDisguisedAsPng = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 };

        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(SingleSlot, executableDisguisedAsPng, "image/png"), CancellationToken.None);

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
            new EnqueueJobCommand(SingleSlot, bytes, contentType), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Jobs.Should().ContainSingle().Which.Status.Should().Be(JobStatus.Queued);
        await _jobs.Received(1).AddAsync(
            Arg.Is<ProcessJob>(job => job.Slot.Id == SingleSlot && job.Status == JobStatus.Queued),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_reports_the_target_size_of_every_job()
    {
        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(SingleSlot, ValidPng, "image/png"), CancellationToken.None);

        var job = result.Value!.Jobs.Should().ContainSingle().Subject;
        job.SlotId.Should().Be(SingleSlot);
        job.Width.Should().Be(SlotDefinition.PcMainSection.Width);
        job.Height.Should().Be(SlotDefinition.PcMainSection.Height);
    }

    [Fact]
    public async Task HandleAsync_stamps_the_job_with_the_injected_clock()
    {
        await CreateSut().HandleAsync(
            new EnqueueJobCommand(SingleSlot, ValidPng, "image/png"), CancellationToken.None);

        _persisted.Should().ContainSingle().Which.CreatedAt.Should().Be(_now);
    }

    [Fact]
    public async Task HandleAsync_writes_jobId_to_processing_queue()
    {
        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(SingleSlot, ValidPng, "image/png"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _queue.Received(1).TryEnqueue(result.Value!.Jobs[0].JobId);
    }

    [Fact]
    public async Task HandleAsync_returns_unavailable_and_marks_job_as_error_when_queue_is_full()
    {
        _queue.TryEnqueue(Arg.Any<Guid>()).Returns(false);

        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(SingleSlot, ValidPng, "image/png"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("optimizer.processing_queue_full");
        var job = _persisted.Should().ContainSingle().Subject;
        job.Status.Should().Be(JobStatus.Error);
        await _jobs.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_creates_one_job_per_output_of_a_bundle()
    {
        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(BundleSlot, ValidPng, "image/png"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Jobs.Select(job => job.SlotId)
            .Should().Equal(SlotDefinition.PcHomeSmall.Id, SlotDefinition.PcHomeWide.Id);
        result.Value.Jobs.Select(job => (job.Width, job.Height))
            .Should().Equal((320, 315), (992, 715));
        _persisted.Should().HaveCount(2);
        _queue.Received(2).TryEnqueue(Arg.Any<Guid>());
    }

    [Fact]
    public async Task HandleAsync_shares_the_same_bytes_between_the_outputs_of_a_bundle()
    {
        await CreateSut().HandleAsync(
            new EnqueueJobCommand(BundleSlot, ValidPng, "image/png"), CancellationToken.None);

        _persisted.Should().HaveCount(2);
        _persisted[0].InputImage.Should().BeSameAs(_persisted[1].InputImage);
        _persisted.Select(job => job.Id).Distinct().Should().HaveCount(2, "cada salida es un trabajo propio");
    }

    [Fact]
    public async Task HandleAsync_fails_the_jobs_already_created_when_the_queue_fills_mid_bundle()
    {
        // El primero entra y el segundo se encuentra la cola llena.
        _queue.TryEnqueue(Arg.Any<Guid>()).Returns(true, false);

        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(BundleSlot, ValidPng, "image/png"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("optimizer.processing_queue_full");
        _persisted.Should().HaveCount(2);
        _persisted.Should().OnlyContain(
            job => job.Status == JobStatus.Error,
            "ningun trabajo del paquete puede quedarse encolado para siempre");
    }

    [Fact]
    public async Task HandleAsync_validates_the_image_once_for_the_whole_bundle()
    {
        var result = await CreateSut().HandleAsync(
            new EnqueueJobCommand(BundleSlot, [0x4D, 0x5A, 0x90], "image/png"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("optimizer.image_content_mismatch");
        _persisted.Should().BeEmpty();
    }
}
