using FluentAssertions;
using LexPCImages.Modules.Optimizer.Infrastructure.Queue;

namespace LexPCImages.UnitTests.Optimizer.Infrastructure;

public sealed class ChannelJobQueueTests
{
    [Fact]
    public void TryEnqueue_returns_false_when_capacity_is_reached()
    {
        var queue = new ChannelJobQueue(1);

        queue.TryEnqueue(Guid.NewGuid()).Should().BeTrue();
        queue.TryEnqueue(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Constructor_rejects_non_positive_capacity()
    {
        var act = () => new ChannelJobQueue(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
