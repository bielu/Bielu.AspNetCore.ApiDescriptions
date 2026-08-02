using System.Threading.Channels;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker.Tests;

/// <summary>
/// An <see cref="IBrokerBridge" /> test double: records what was published, and replays a scripted
/// message sequence to a tail. Nothing here talks to a broker, so the endpoint tests exercise the
/// proxy's own behaviour rather than a driver's.
/// </summary>
internal sealed class FakeBrokerBridge : IBrokerBridge
{
    private readonly Channel<BrokerMessage> _tail = Channel.CreateUnbounded<BrokerMessage>();

    public List<BrokerPublishRequest> Published { get; } = [];

    public bool Disposed { get; private set; }

    /// <summary>Thrown by the next <see cref="PublishAsync" />, when set.</summary>
    public Exception? PublishFailure { get; set; }

    /// <summary>Queues a message for the tail stream to emit.</summary>
    public void Emit(BrokerMessage message) => _tail.Writer.TryWrite(message);

    /// <summary>Ends the tail stream, as a driver would when the subscription closes.</summary>
    public void CompleteTail() => _tail.Writer.TryComplete();

    public Task<BrokerPublishReceipt> PublishAsync(BrokerPublishRequest request, CancellationToken cancellationToken)
    {
        if (PublishFailure is not null)
        {
            return Task.FromException<BrokerPublishReceipt>(PublishFailure);
        }

        Published.Add(request);
        return Task.FromResult(new BrokerPublishReceipt(
            request.Channel,
            new DateTimeOffset(2026, 8, 2, 10, 0, 0, TimeSpan.Zero),
            Partition: 3,
            Offset: 42));
    }

    public IAsyncEnumerable<BrokerMessage> TailAsync(BrokerTailRequest request, CancellationToken cancellationToken) =>
        _tail.Reader.ReadAllAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
