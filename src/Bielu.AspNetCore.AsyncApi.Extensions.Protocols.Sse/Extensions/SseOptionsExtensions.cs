using Bielu.AspNetCore.AsyncApi.Services;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse;

/// <summary>
/// Fluent helpers for registering SSE protocol bindings on <see cref="AsyncApiOptions"/>.
/// </summary>
public static class SseOptionsExtensions
{
    /// <summary>
    /// Adds a <see cref="SseChannelBinding"/> for the channel identified by <paramref name="channelName"/>.
    /// </summary>
    /// <param name="options">The AsyncAPI options being configured.</param>
    /// <param name="channelName">The channel key the binding applies to (the event-stream endpoint).</param>
    /// <param name="configure">An optional callback to configure the binding.</param>
    public static AsyncApiOptions AddSseChannelBinding(
        this AsyncApiOptions options,
        string channelName,
        Action<SseChannelBinding>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        var binding = new SseChannelBinding();
        configure?.Invoke(binding);
        return options.AddChannelBinding(channelName, binding);
    }

    /// <summary>
    /// Adds a <see cref="SseOperationBinding"/> for the operation identified by <paramref name="operationName"/>.
    /// </summary>
    /// <param name="options">The AsyncAPI options being configured.</param>
    /// <param name="operationName">The operation key the binding applies to (subscribing to the stream).</param>
    /// <param name="configure">An optional callback to configure the binding.</param>
    public static AsyncApiOptions AddSseOperationBinding(
        this AsyncApiOptions options,
        string operationName,
        Action<SseOperationBinding>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        var binding = new SseOperationBinding();
        configure?.Invoke(binding);
        return options.AddOperationBinding(operationName, binding);
    }
}
