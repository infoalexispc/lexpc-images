using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Ports;
using LexPCImages.Modules.Optimizer.Application.UseCases.GetJobDownload;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Shared.Common.Errors;
using NSubstitute;

namespace LexPCImages.UnitTests.Optimizer.Application;

public sealed class GetJobDownloadHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);

    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly GetJobDownloadHandler _sut;

    public GetJobDownloadHandlerTests()
    {
        _sut = new GetJobDownloadHandler(_jobs);
    }

    private static ProcessJob DoneJob(SlotDefinition slot, byte[] output, string contentType)
    {
        var job = ProcessJob.Create(slot, [0x01], "image/png", Now);
        job.MarkProcessing(ProcessingStage.Encoding, 92, Now);
        job.MarkDone(output, contentType, Now);
        return job;
    }

    [Fact]
    public async Task HandleAsync_returns_not_found_for_unknown_job()
    {
        var jobId = Guid.NewGuid();
        _jobs.GetAsync(jobId, Arg.Any<CancellationToken>()).Returns((ProcessJob?)null);

        var result = await _sut.HandleAsync(new GetJobDownloadQuery(jobId), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_returns_conflict_when_job_is_not_done()
    {
        var job = ProcessJob.Create(SlotDefinition.PcHomeSmall, [0x01], "image/png", Now);
        _jobs.GetAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await _sut.HandleAsync(new GetJobDownloadQuery(job.Id), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("optimizer.job_not_ready");
    }

    [Fact]
    public async Task HandleAsync_returns_output_when_job_is_done()
    {
        var output = new byte[] { 0x52, 0x49, 0x46, 0x46 };
        var job = DoneJob(SlotDefinition.PcHomeSmall, output, "image/webp");
        _jobs.GetAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await _sut.HandleAsync(new GetJobDownloadQuery(job.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().Equal(output);
        result.Value.ContentType.Should().Be("image/webp");
        result.Value.FileName.Should().Be($"{SlotDefinition.PcHomeSmall.Id.Value}-{job.Id:N}.webp");
    }

    [Fact]
    public async Task HandleAsync_names_the_file_after_the_slot_that_produced_it()
    {
        var job = DoneJob(SlotDefinition.PcMainSection, [0x52, 0x49], "image/webp");
        _jobs.GetAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await _sut.HandleAsync(new GetJobDownloadQuery(job.Id), CancellationToken.None);

        result.Value!.FileName.Should().StartWith(SlotDefinition.PcMainSection.Id.Value);
        result.Value.FileName.Should().NotContain("pc-home-");
    }

    [Fact]
    public async Task HandleAsync_derives_the_extension_from_the_output_content_type()
    {
        var job = DoneJob(SlotDefinition.PcHomeSmall, [0x89, 0x50], "image/png");
        _jobs.GetAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await _sut.HandleAsync(new GetJobDownloadQuery(job.Id), CancellationToken.None);

        result.Value!.FileName.Should().EndWith(".png");
    }
}
