namespace Bielu.AspNetCore.AsyncApi.Scalar;

/// <summary>
/// Base configuration for a Scalar console plugin: the AsyncAPI documents the client-side bundle
/// scans for the plugin's protocol bindings. Protocol packages derive a named options class from
/// this so their public API stays protocol-flavoured while the behaviour lives here.
/// </summary>
/// <typeparam name="TSelf">The derived options type, so <see cref="AddDocument"/> chains as the derived type.</typeparam>
public abstract class ScalarPluginDocumentOptions<TSelf>
    where TSelf : ScalarPluginDocumentOptions<TSelf>
{
    /// <summary>
    /// The AsyncAPI documents (name and URL) the console scans for the plugin's bindings.
    /// </summary>
    public IList<ScalarAsyncApiDocument> Documents { get; } = new List<ScalarAsyncApiDocument>();

    /// <summary>
    /// Adds an AsyncAPI document to scan.
    /// </summary>
    /// <param name="name">The logical document name (typically matches the AsyncAPI document name).</param>
    /// <param name="url">The URL the AsyncAPI JSON document is served from (relative or absolute).</param>
    /// <returns>The same options instance for chaining.</returns>
    public TSelf AddDocument(string name, string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(url);

        Documents.Add(new ScalarAsyncApiDocument(name, url));
        return (TSelf)this;
    }
}
