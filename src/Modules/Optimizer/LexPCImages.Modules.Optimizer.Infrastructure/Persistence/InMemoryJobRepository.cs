using System.Collections.Concurrent;
using LexPCImages.Modules.Optimizer.Domain.Abstractions;
using LexPCImages.Modules.Optimizer.Domain.Entities;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Persistence;

public sealed class InMemoryJobRepository : IJobRepository
{
    private readonly ConcurrentDictionary<Guid, ProcessJob> _jobs = new();

    public Task<ProcessJob> AddAsync(ProcessJob job, CancellationToken cancellationToken)
    {
        if (!_jobs.TryAdd(job.Id, job))
        {
            throw new InvalidOperationException($"Job {job.Id} already exists.");
        }
        return Task.FromResult(job);
    }

    public Task<ProcessJob?> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<IReadOnlyList<ProcessJob>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ProcessJob> snapshot = _jobs.Values.ToList();
        return Task.FromResult(snapshot);
    }

    public Task DeleteAsync(Guid jobId, CancellationToken cancellationToken)
    {
        _jobs.TryRemove(jobId, out _);
        return Task.CompletedTask;
    }
}
