namespace Bielu.AspNetCore.AsyncApi.LikeC4;

/// <summary>An AsyncAPI document to describe in a LikeC4 model, as one service.</summary>
/// <example>
/// <code>
/// var source = new AsyncApiLikeC4Source("orders", File.ReadAllText("orders.asyncapi.json"))
/// {
///     Link = "https://orders.example.com/asyncapi/v1.json",
/// };
/// </code>
/// </example>
public sealed class AsyncApiLikeC4Source
{
    /// <summary>Creates a source.</summary>
    /// <param name="name">
    /// The service's name, such as <c>orders</c>. It gives the service element its id (camelCased, so <c>orders-api</c>
    /// becomes <c>ordersApi</c>) and is written to its <c>asyncapi</c> metadata.
    /// </param>
    /// <param name="document">The AsyncAPI 2.x or 3.x document, as JSON or YAML.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    public AsyncApiLikeC4Source(string name, string document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(document);
        Name = name;
        Document = document;
    }

    /// <summary>The service's name.</summary>
    public string Name { get; }

    /// <summary>The AsyncAPI document, as JSON or YAML.</summary>
    public string Document { get; }

    /// <summary>
    /// Where readers find the document, such as its URL or a path relative to the model file; the service element
    /// links to it. A link with whitespace in it is left out.
    /// </summary>
    public string? Link { get; init; }
}
