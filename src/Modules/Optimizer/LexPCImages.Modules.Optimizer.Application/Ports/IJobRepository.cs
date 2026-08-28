using LexPCImages.Modules.Optimizer.Domain.Entities;

namespace LexPCImages.Modules.Optimizer.Application.Ports;

/// <summary>
/// Persistencia de trabajos. Toda modificación del agregado debe confirmarse con
/// <see cref="UpdateAsync"/>: las implementaciones no pueden asumir que el llamante y el
/// almacén comparten la misma instancia.
/// </summary>
public interface IJobRepository
{
    Task AddAsync(ProcessJob job, CancellationToken cancellationToken);

    Task<ProcessJob?> GetAsync(Guid jobId, CancellationToken cancellationToken);

    Task UpdateAsync(ProcessJob job, CancellationToken cancellationToken);
}
