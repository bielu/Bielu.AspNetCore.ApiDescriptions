namespace Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Aspire;

/// <summary>
/// Configuration for the interactive Scalar gRPC console in an Aspire AppHost.
/// </summary>
public sealed class ScalarGrpcAspireOptions
{
    /// <summary>
    /// The AsyncAPI documents (name and URL) the gRPC console scans for services and methods.
    /// URLs are evaluated in the browser, so they must be reachable from the Scalar container's page.
    /// </summary>
    public IList<ScalarGrpcAspireDocument> Documents { get; } = new List<ScalarGrpcAspireDocument>();

    /// <summary>
    /// Adds an AsyncAPI document to scan for gRPC services.
    /// </summary>
    /// <param name="name">The logical document name.</param>
    /// <param name="url">The URL the AsyncAPI JSON document is served from.</param>
    /// <returns>The same <see cref="ScalarGrpcAspireOptions"/> instance for chaining.</returns>
    public ScalarGrpcAspireOptions AddDocument(string name, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        Documents.Add(new ScalarGrpcAspireDocument(name, url));
        return this;
    }
}

/// <summary>
/// A reference to an AsyncAPI document served by an Aspire resource.
/// </summary>
/// <param name="Name">The logical document name.</param>
/// <param name="Url">The URL the AsyncAPI JSON document is served from.</param>
public sealed record ScalarGrpcAspireDocument(string Name, string Url);
