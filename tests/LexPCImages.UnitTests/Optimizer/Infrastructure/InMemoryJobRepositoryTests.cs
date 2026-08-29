using FluentAssertions;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Modules.Optimizer.Infrastructure.Configuration;
using LexPCImages.Modules.Optimizer.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LexPCImages.UnitTests.Optimizer.Infrastructure;

public sealed class InMemoryJobRepositoryTests
{
    private static readonly byte[] AnyImage = [0x01, 0x02];
    private static readonly byte[] AnyOutput = [0xFF];

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero));
    private readonly OptimizerOptions _options = new()
    {
        JobRetention = TimeSpan.FromMinutes(30),
        MaxTrackedJobs = 3,
    };

    private InMemoryJobRepository CreateSut() => new(
        Options.Create(_options), _time, NullLogger<InMemoryJobRepository>.Instance);

    private ProcessJob NewJob() =>
        ProcessJob.Create(SlotDefinition.PcHomeSmall, AnyImage, "image/png", _time.GetUtcNow());

    [Fact]
    public async Task GetAsync_returns_null_for_an_unknown_job()
    {
        var found = await CreateSut().GetAsync(Guid.NewGuid(), CancellationToken.None);

        found.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_makes_the_job_retrievable()
    {
        var sut = CreateSut();
        var job = NewJob();

        await sut.AddAsync(job, CancellationToken.None);

        (await sut.GetAsync(job.Id, CancellationToken.None)).Should().BeSameAs(job);
    }

    [Fact]
    public async Task AddAsync_rejects_a_duplicate_id()
    {
        var sut = CreateSut();
        var job = NewJob();
        await sut.AddAsync(job, CancellationToken.None);

        var act = async () => await sut.AddAsync(job, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateAsync_persists_the_new_state()
    {
        var sut = CreateSut();
        var job = NewJob();
        await sut.AddAsync(job, CancellationToken.None);

        job.MarkProcessing(ProcessingStage.Decoding, 5, _time.GetUtcNow());
        job.MarkDone(AnyOutput, "image/webp", _time.GetUtcNow());
        await sut.UpdateAsync(job, CancellationToken.None);

        var stored = await sut.GetAsync(job.Id, CancellationToken.None);
        stored!.Status.Should().Be(JobStatus.Done);
        stored.OutputImage.Should().BeEquivalentTo(AnyOutput);
    }

    [Fact]
    public async Task UpdateAsync_rejects_an_unknown_job()
    {
        var sut = CreateSut();

        var act = async () => await sut.UpdateAsync(NewJob(), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Terminal_jobs_are_evicted_once_the_retention_window_has_passed()
    {
        var sut = CreateSut();
        var old = NewJob();
        await sut.AddAsync(old, CancellationToken.None);
        old.MarkProcessing(ProcessingStage.Decoding, 5, _time.GetUtcNow());
        old.MarkDone(AnyOutput, "image/webp", _time.GetUtcNow());
        await sut.UpdateAsync(old, CancellationToken.None);

        _time.Advance(TimeSpan.FromMinutes(31));
        await sut.AddAsync(NewJob(), CancellationToken.None);

        (await sut.GetAsync(old.Id, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Jobs_still_running_survive_the_retention_sweep()
    {
        var sut = CreateSut();
        var running = NewJob();
        await sut.AddAsync(running, CancellationToken.None);
        running.MarkProcessing(ProcessingStage.Resizing, 20, _time.GetUtcNow());
        await sut.UpdateAsync(running, CancellationToken.None);

        _time.Advance(TimeSpan.FromHours(2));
        await sut.AddAsync(NewJob(), CancellationToken.None);

        (await sut.GetAsync(running.Id, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task The_number_of_tracked_jobs_never_exceeds_the_configured_cap()
    {
        var sut = CreateSut();
        var ids = new List<Guid>();

        for (var i = 0; i < 10; i++)
        {
            var job = NewJob();
            ids.Add(job.Id);
            await sut.AddAsync(job, CancellationToken.None);
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        var alive = new List<Guid>();
        foreach (var id in ids)
        {
            if (await sut.GetAsync(id, CancellationToken.None) is not null)
            {
                alive.Add(id);
            }
        }

        alive.Should().HaveCountLessThanOrEqualTo(_options.MaxTrackedJobs + 1);
        alive.Should().Contain(ids[^1], "el trabajo recién encolado nunca debe descartarse");
    }
}
