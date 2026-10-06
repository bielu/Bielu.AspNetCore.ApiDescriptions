using Bielu.AspNetCore.AsyncApi.Services;

namespace Bielu.AspNetCore.AsyncApi.Wolverine;

/// <summary>
/// Wolverine integration for <see cref="AsyncApiOptions"/>.
/// </summary>
public static class WolverineAsyncApiOptionsExtensions
{
    /// <summary>
    /// Documents the messages Wolverine routes to external transports. For every message type in the
    /// configured assemblies, each Wolverine route becomes a <c>send</c> operation on the channel of the
    /// route's endpoint; every listening endpoint with a default incoming message type becomes a
    /// <c>receive</c> operation. Messages are named by Wolverine's message type name (honouring
    /// <c>[MessageIdentity]</c>), so the document uses the same names as the wire.
    /// </summary>
    /// <param name="options">The AsyncAPI options for one document.</param>
    /// <param name="configure">Optional callback to choose assemblies, transports and servers.</param>
    /// <returns>The same options, for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddAsyncApi("realtime", options =>
    /// {
    ///     options.AddServer("signalr", "localhost:5000", "signalr");
    ///     options.AddWolverine(wolverine => wolverine
    ///         .IncludeAssemblyContaining&lt;OrderShipped&gt;()
    ///         .IncludeSchemes("signalr"));
    /// });
    /// </code>
    /// </example>
    public static AsyncApiOptions AddWolverine(this AsyncApiOptions options, Action<WolverineAsyncApiOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var wolverineOptions = new WolverineAsyncApiOptions();
        configure?.Invoke(wolverineOptions);

        return options.AddDocumentTransformer(new WolverineDocumentTransformer(wolverineOptions));
    }
}
