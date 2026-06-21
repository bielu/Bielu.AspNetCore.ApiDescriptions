using Bielu.AspNetCore.AsyncApi.Services;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;

/// <summary>
/// Fluent helpers for registering SignalR protocol bindings on <see cref="AsyncApiOptions"/>.
/// </summary>
public static class SignalROptionsExtensions
{
    /// <summary>
    /// Adds a <see cref="SignalRChannelBinding"/> for the channel identified by <paramref name="channelName"/>.
    /// </summary>
    /// <param name="options">The AsyncAPI options being configured.</param>
    /// <param name="channelName">The channel key the binding applies to (the SignalR hub).</param>
    /// <param name="configure">An optional callback to configure the binding.</param>
    public static AsyncApiOptions AddSignalRChannelBinding(
        this AsyncApiOptions options,
        string channelName,
        Action<SignalRChannelBinding>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        var binding = new SignalRChannelBinding();
        configure?.Invoke(binding);
        return options.AddChannelBinding(channelName, binding);
    }

    /// <summary>
    /// Adds a <see cref="SignalROperationBinding"/> for the operation identified by <paramref name="operationName"/>.
    /// </summary>
    /// <param name="options">The AsyncAPI options being configured.</param>
    /// <param name="operationName">The operation key the binding applies to (a hub method).</param>
    /// <param name="configure">An optional callback to configure the binding.</param>
    public static AsyncApiOptions AddSignalROperationBinding(
        this AsyncApiOptions options,
        string operationName,
        Action<SignalROperationBinding>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        var binding = new SignalROperationBinding();
        configure?.Invoke(binding);
        return options.AddOperationBinding(operationName, binding);
    }
}
