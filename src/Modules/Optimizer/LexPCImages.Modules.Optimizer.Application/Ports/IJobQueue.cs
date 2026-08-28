namespace LexPCImages.Modules.Optimizer.Application.Ports;

public interface IJobQueueWriter
{
    bool TryEnqueue(Guid jobId);
}

public interface IJobQueueReader
{
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}
