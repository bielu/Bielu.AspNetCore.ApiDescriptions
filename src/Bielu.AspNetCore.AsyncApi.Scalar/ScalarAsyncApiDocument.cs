namespace Bielu.AspNetCore.AsyncApi.Scalar;

/// <summary>
/// A reference to an AsyncAPI document served by the application, scanned by a Scalar console
/// plugin (SignalR, gRPC, ...) for its protocol bindings.
/// </summary>
/// <param name="Name">The logical document name (typically matches the AsyncAPI document name).</param>
/// <param name="Url">The URL the AsyncAPI JSON document is served from (relative or absolute).</param>
public sealed record ScalarAsyncApiDocument(string Name, string Url);
