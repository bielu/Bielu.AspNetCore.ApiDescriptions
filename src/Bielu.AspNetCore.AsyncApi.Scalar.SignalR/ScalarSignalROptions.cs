namespace Bielu.AspNetCore.AsyncApi.Scalar.SignalR;

/// <summary>
/// Configuration for the interactive Scalar SignalR console. Describes which AsyncAPI documents
/// the client should scan for SignalR bindings.
/// </summary>
public sealed class ScalarSignalROptions
{
    /// <summary>
    /// The AsyncAPI documents (name and URL) scanned for SignalR hubs and operations.
    /// </summary>
    public IList<ScalarSignalRDocument> Documents { get; } = new List<ScalarSignalRDocument>();

    /// <summary>
    /// Adds an AsyncAPI document to scan for SignalR hubs.
    /// </summary>
    /// <param name="name">The logical document name (typically matches the AsyncAPI document name).</param>
    /// <param name="url">The URL the AsyncAPI JSON document is served from (relative or absolute).</param>
    /// <returns>The same <see cref="ScalarSignalROptions"/> instance for chaining.</returns>
    public ScalarSignalROptions AddDocument(string name, string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(url);

        Documents.Add(new ScalarSignalRDocument(name, url));
        return this;
    }
}

/// <summary>
/// A reference to an AsyncAPI document served by the application.
/// </summary>
/// <param name="Name">The logical document name.</param>
/// <param name="Url">The URL the AsyncAPI JSON document is served from.</param>
public sealed record ScalarSignalRDocument(string Name, string Url);
