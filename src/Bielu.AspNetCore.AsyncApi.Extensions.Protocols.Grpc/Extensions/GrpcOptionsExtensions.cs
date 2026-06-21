using Bielu.AspNetCore.AsyncApi.Services;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc;

/// <summary>
/// Fluent helpers for registering gRPC protocol bindings on <see cref="AsyncApiOptions"/>.
/// </summary>
/// <remarks>
/// Channel and operation bindings are keyed by name and resolved through
/// <c>[Channel(BindingsRef=...)]</c> / <c>[PublishOperation(BindingsRef=...)]</c>, mirroring the
/// SignalR extension. Server bindings are attached directly via <c>AddServer(...)</c> and message
/// bindings via the binding classes, because the core options only key channel and operation bindings.
/// </remarks>
public static class GrpcOptionsExtensions
{
    /// <summary>
    /// Adds a <see cref="GrpcChannelBinding"/> for the channel identified by <paramref name="channelName"/>.
    /// </summary>
    /// <param name="options">The AsyncAPI options being configured.</param>
    /// <param name="channelName">The channel key the binding applies to (the gRPC service).</param>
    /// <param name="configure">An optional callback to configure the binding.</param>
    public static AsyncApiOptions AddGrpcChannelBinding(
        this AsyncApiOptions options,
        string channelName,
        Action<GrpcChannelBinding>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        var binding = new GrpcChannelBinding();
        configure?.Invoke(binding);
        return options.AddChannelBinding(channelName, binding);
    }

    /// <summary>
    /// Adds a <see cref="GrpcOperationBinding"/> for the operation identified by <paramref name="operationName"/>.
    /// </summary>
    /// <param name="options">The AsyncAPI options being configured.</param>
    /// <param name="operationName">The operation key the binding applies to (an RPC method).</param>
    /// <param name="configure">An optional callback to configure the binding.</param>
    public static AsyncApiOptions AddGrpcOperationBinding(
        this AsyncApiOptions options,
        string operationName,
        Action<GrpcOperationBinding>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        var binding = new GrpcOperationBinding();
        configure?.Invoke(binding);
        return options.AddOperationBinding(operationName, binding);
    }
}
