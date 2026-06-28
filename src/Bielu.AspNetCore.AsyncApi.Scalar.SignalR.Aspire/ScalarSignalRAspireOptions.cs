namespace Bielu.AspNetCore.AsyncApi.Scalar.SignalR.Aspire;

/// <summary>
/// Configuration for the interactive Scalar SignalR console in an Aspire AppHost.
/// </summary>
public sealed class ScalarSignalRAspireOptions
{
    /// <summary>
    /// The AsyncAPI documents (name and URL) the SignalR console scans for hubs and operations.
    /// URLs are evaluated in the browser, so they must be reachable from the Scalar container's page.
    /// </summary>
    public IList<ScalarSignalRAspireDocument> Documents { get; } = new List<ScalarSignalRAspireDocument>();

    /// <summary>
    /// Adds an AsyncAPI document to scan for SignalR hubs.
    /// </summary>
    /// <param name="name">The logical document name.</param>
    /// <param name="url">The URL the AsyncAPI JSON document is served from.</param>
    /// <returns>The same <see cref="ScalarSignalRAspireOptions"/> instance for chaining.</returns>
    public ScalarSignalRAspireOptions AddDocument(string name, string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(url);

        Documents.Add(new ScalarSignalRAspireDocument(name, url));
        return this;
    }
}

/// <summary>
/// A reference to an AsyncAPI document served by an Aspire resource.
/// </summary>
/// <param name="Name">The logical document name.</param>
/// <param name="Url">The URL the AsyncAPI JSON document is served from.</param>
public sealed record ScalarSignalRAspireDocument(string Name, string Url);
