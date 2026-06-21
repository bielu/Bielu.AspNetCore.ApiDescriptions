using Bielu.AspNetCore.AsyncApi.Services;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc;

/// <summary>
/// Fluent helpers for registering WebRTC protocol bindings on <see cref="AsyncApiOptions"/>.
/// </summary>
public static class WebRtcOptionsExtensions
{
    /// <summary>
    /// Adds a <see cref="WebRtcChannelBinding"/> for the channel identified by <paramref name="channelName"/>.
    /// </summary>
    /// <param name="options">The AsyncAPI options being configured.</param>
    /// <param name="channelName">The channel key the binding applies to (a data channel or media track).</param>
    /// <param name="configure">An optional callback to configure the binding.</param>
    public static AsyncApiOptions AddWebRtcChannelBinding(
        this AsyncApiOptions options,
        string channelName,
        Action<WebRtcChannelBinding>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        var binding = new WebRtcChannelBinding();
        configure?.Invoke(binding);
        return options.AddChannelBinding(channelName, binding);
    }

    /// <summary>
    /// Adds a <see cref="WebRtcOperationBinding"/> for the operation identified by <paramref name="operationName"/>.
    /// </summary>
    /// <param name="options">The AsyncAPI options being configured.</param>
    /// <param name="operationName">The operation key the binding applies to (a signaling exchange or channel send/receive).</param>
    /// <param name="configure">An optional callback to configure the binding.</param>
    public static AsyncApiOptions AddWebRtcOperationBinding(
        this AsyncApiOptions options,
        string operationName,
        Action<WebRtcOperationBinding>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        var binding = new WebRtcOperationBinding();
        configure?.Invoke(binding);
        return options.AddOperationBinding(operationName, binding);
    }
}
