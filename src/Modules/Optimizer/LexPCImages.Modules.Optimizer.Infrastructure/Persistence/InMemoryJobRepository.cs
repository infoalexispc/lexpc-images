using System.Collections.Concurrent;
using LexPCImages.Modules.Optimizer.Application.Ports;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Persistence;

/// <summary>
/// Almacén volátil de trabajos.
/// <para>
/// <see cref="UpdateAsync"/> reescribe la entrada de forma explícita. Antes solo comprobaba que
/// la clave existiera y el pipeline funcionaba por casualidad, porque el agregado se compartía
/// por referencia: cualquier implementación real habría perdido todas las actualizaciones.
/// La escritura sobre <see cref="ConcurrentDictionary{TKey,TValue}"/> además publica los cambios
/// del hilo de fondo hacia los hilos que atienden peticiones HTTP.
/// </para>
/// <para>
/// Los trabajos terminados se descartan pasada la retención configurada: guardan la imagen de
/// entrada y la de salida, así que conservarlos indefinidamente agota la memoria del proceso.
/// </para>
/// </summary>
public sealed class InMemoryJobRepository : IJobRepository
{
    private readonly ConcurrentDictionary<Guid, ProcessJob> _jobs = new();
    private readonly OptimizerOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<InMemoryJobRepository> _logger;

    public InMemoryJobRepository(
        IOptions<OptimizerOptions> options,
        TimeProvider time,
        ILogger<InMemoryJobRepository> logger)
    {
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    public Task AddAsync(ProcessJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        EvictExpired();
        if (!_jobs.TryAdd(job.Id, job))
        {
            throw new InvalidOperationException($"Job {job.Id} already exists.");
        }
        return Task.CompletedTask;
    }

    public Task<ProcessJob?> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task UpdateAsync(ProcessJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (!_jobs.ContainsKey(job.Id))
        {
            throw new KeyNotFoundException($"Job {job.Id} was not found.");
        }
        _jobs[job.Id] = job;
        return Task.CompletedTask;
    }

    /// <summary>Descarta trabajos terminados que superan la retención, y los más antiguos si se excede el tope.</summary>
    private void EvictExpired()
    {
        var now = _time.GetUtcNow();
        var threshold = now - _options.JobRetention;
        var evicted = 0;

        foreach (var entry in _jobs)
        {
            var job = entry.Value;
            var closedAt = job.CompletedAt ?? job.CreatedAt;
            if (job.IsTerminal && closedAt <= threshold && _jobs.TryRemove(entry.Key, out _))
            {
                evicted++;
            }
        }

        var overflow = _jobs.Count - _options.MaxTrackedJobs;
        if (overflow > 0)
        {
            // Se sacrifican primero los ya terminados: descartar uno en curso dejaría al worker
            // sin trabajo que procesar y al cliente sin respuesta.
            var expendableFirst = _jobs
                .OrderByDescending(entry => entry.Value.IsTerminal)
                .ThenBy(entry => entry.Value.CompletedAt ?? entry.Value.CreatedAt)
                .Take(overflow);
            foreach (var entry in expendableFirst)
            {
                if (_jobs.TryRemove(entry.Key, out _))
                {
                    evicted++;
                }
            }
        }

        if (evicted > 0)
        {
            _logger.LogDebug("Evicted {Count} job(s); {Remaining} still tracked", evicted, _jobs.Count);
        }
    }
}
