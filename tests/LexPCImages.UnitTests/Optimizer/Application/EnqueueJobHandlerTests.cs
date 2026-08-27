using System.Threading.Channels;
using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.UseCases.EnqueueJob;
using LexPCImages.Modules.Optimizer.Domain.Abstractions;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Shared.Common.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LexPCImages.UnitTests.Optimizer.Application;

public sealed class EnqueueJobHandlerTests
{
    private static readonly SlotId HomeSlot = SlotDefinition.PcHome.Id;
    private static readonly byte[] ValidImage = new byte[] { 0x01, 0x02, 0x03 };
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly ISlotRegistry _slots = Substitute.For<ISlotRegistry>();
    private readonly TimeProvider _time = Substitute.For<TimeProvider>();
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
    private readonly DateTimeOffset _now = new(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);

    private EnqueueJobHandler CreateSut() => new(_jobs, _slots, _channel, NullLogger<EnqueueJobHandler>.Instance, _time);

    public EnqueueJobHandlerTests()
    {
        _time.GetUtcNow().Returns(_now);
        _slots.FindById(HomeSlot).Returns(SlotDefinition.PcHome);
    }

    [Fact]
    public async Task HandleAsync_returns_SlotNotFound_when_slot_unknown()
    {
        _slots.FindById(Arg.Any<SlotId>()).Returns((SlotDefinition?)null);
        var sut = CreateSut();

        var result = await sut.HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidImage, "image/png"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("optimizer.slot_not_found");
    }

    [Fact]
    public async Task HandleAsync_returns_ImageEmpty_when_bytes_are_zero()
    {
        var sut = CreateSut();

        var result = await sut.HandleAsync(
            new EnqueueJobCommand(HomeSlot, Array.Empty<byte>(), "image/png"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("optimizer.image_empty");
    }

    [Fact]
    public async Task HandleAsync_returns_ImageTooLarge_when_exceeding_max()
    {
        var sut = CreateSut();
        var hugeImage = new byte[ProcessJob.MaxInputBytes + 1];

        var result = await sut.HandleAsync(
            new EnqueueJobCommand(HomeSlot, hugeImage, "image/png"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("optimizer.image_too_large");
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    public async Task HandleAsync_rejects_unsupported_content_types(string contentType)
    {
        var sut = CreateSut();

        var result = await sut.HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidImage, contentType),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("optimizer.image_format_not_supported");
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public async Task HandleAsync_accepts_supported_content_types(string contentType)
    {
        _jobs.AddAsync(Arg.Any<ProcessJob>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<ProcessJob>(0));
        var sut = CreateSut();

        var result = await sut.HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidImage, contentType),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(JobStatus.Queued);
        await _jobs.Received(1).AddAsync(
            Arg.Is<ProcessJob>(j => j.Slot.Id == HomeSlot && j.Status == JobStatus.Queued),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_writes_jobId_to_processing_channel()
    {
        _jobs.AddAsync(Arg.Any<ProcessJob>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<ProcessJob>(0));

        var sut = CreateSut();
        var result = await sut.HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidImage, "image/png"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var hasItem = _channel.Reader.TryRead(out var written);
        hasItem.Should().BeTrue();
        written.Should().Be(result.Value!.JobId);
    }

    [Fact]
    public async Task HandleAsync_applies_overrides_to_the_persisted_job()
    {
        ProcessJob? persisted = null;
        _jobs.AddAsync(Arg.Any<ProcessJob>(), Arg.Any<CancellationToken>())
            .Returns(call => { persisted = call.ArgAt<ProcessJob>(0); return persisted; });

        var overrides = new RefinementOverrides(
            SuppressShadow: false,
            RemoveDesk: false,
            ProtectLegs: true,
            CropMarginPct: 0.12);

        var sut = CreateSut();
        var result = await sut.HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidImage, "image/png", overrides),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.EffectiveRefinement.SuppressShadow.Should().BeFalse();
        persisted.EffectiveRefinement.RemoveDesk.Should().BeFalse();
        persisted.EffectiveRefinement.ProtectLegs.Should().BeTrue();
        persisted.EffectiveRefinement.CropMarginPct.Should().Be(0.12);
    }

    [Fact]
    public async Task HandleAsync_uses_slot_defaults_when_no_overrides_provided()
    {
        ProcessJob? persisted = null;
        _jobs.AddAsync(Arg.Any<ProcessJob>(), Arg.Any<CancellationToken>())
            .Returns(call => { persisted = call.ArgAt<ProcessJob>(0); return persisted; });

        var sut = CreateSut();
        var result = await sut.HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidImage, "image/png"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.EffectiveRefinement.Should().Be(SlotDefinition.PcHome.EffectiveRefinement);
    }

    [Fact]
    public async Task HandleAsync_partial_overrides_only_change_specified_fields()
    {
        ProcessJob? persisted = null;
        _jobs.AddAsync(Arg.Any<ProcessJob>(), Arg.Any<CancellationToken>())
            .Returns(call => { persisted = call.ArgAt<ProcessJob>(0); return persisted; });

        var overrides = new RefinementOverrides(SuppressShadow: false);

        var sut = CreateSut();
        var result = await sut.HandleAsync(
            new EnqueueJobCommand(HomeSlot, ValidImage, "image/png", overrides),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.EffectiveRefinement.SuppressShadow.Should().BeFalse();
        persisted.EffectiveRefinement.RemoveDesk.Should().BeTrue();
        persisted.EffectiveRefinement.ProtectLegs.Should().BeTrue();
    }
}
