using System.Threading.Channels;
using LexPCImages.Modules.Optimizer.Application.Ports;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Queue;

public sealed class ChannelJobQueue : IJobQueueWriter, IJobQueueReader
{
    private readonly Channel<Guid> _channel;

    public ChannelJobQueue(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Queue capacity must be positive.");
        }

        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public bool TryEnqueue(Guid jobId) => _channel.Writer.TryWrite(jobId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
