using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.UseCases.GetJobStatus;
using LexPCImages.Modules.Optimizer.Domain.Abstractions;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Shared.Common.Errors;
using NSubstitute;

namespace LexPCImages.UnitTests.Optimizer.Application;

public sealed class GetJobStatusHandlerTests
{
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly Guid _jobId = Guid.NewGuid();
    private readonly SlotDefinition _slot = SlotDefinition.PcHome;

    private GetJobStatusHandler CreateSut() => new(_jobs);

    [Fact]
    public async Task HandleAsync_returns_JobNotFound_when_repository_returns_null()
    {
        _jobs.GetAsync(_jobId, Arg.Any<CancellationToken>()).Returns((ProcessJob?)null);
        var sut = CreateSut();

        var result = await sut.HandleAsync(new GetJobStatusQuery(_jobId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("optimizer.job_not_found");
    }

    [Fact]
    public async Task HandleAsync_maps_entity_to_JobStatusResult()
    {
        var now = DateTimeOffset.UtcNow;
        var job = ProcessJob.Create(_slot, new byte[] { 0x01 }, "image/png", now);
        job.MarkProcessing(ProcessingStage.Inferring, 50);

        _jobs.GetAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);
        var sut = CreateSut();

        var result = await sut.HandleAsync(new GetJobStatusQuery(job.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(JobStatus.Processing);
        result.Value.Stage.Should().Be(ProcessingStage.Inferring);
        result.Value.Progress.Should().Be(50);
        result.Value.JobId.Should().Be(job.Id);
    }
}
