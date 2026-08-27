using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Domain.Abstractions;

public interface IJobRepository
{
    Task<ProcessJob> AddAsync(ProcessJob job, CancellationToken cancellationToken);
    Task<ProcessJob?> GetAsync(Guid jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProcessJob>> ListAsync(CancellationToken cancellationToken);
    Task DeleteAsync(Guid jobId, CancellationToken cancellationToken);
}

public interface ISlotRegistry
{
    SlotDefinition? FindById(SlotId id);
    IReadOnlyList<SlotDefinition> All();
}
