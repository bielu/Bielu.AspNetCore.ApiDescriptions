namespace Bielu.AspNetCore.AsyncApi.LikeC4;

/// <summary>An Arazzo document whose workflows to draw as LikeC4 dynamic views, one per workflow.</summary>
/// <example>
/// <code>
/// var workflows = new ArazzoLikeC4Source("checkout", File.ReadAllText("checkout.arazzo.yaml"));
/// </code>
/// </example>
public sealed class ArazzoLikeC4Source
{
    /// <summary>Creates a source.</summary>
    /// <param name="name">
    /// The document's name, such as <c>checkout</c>. Errors name the document by it, and a step of another document
    /// invoking <c>$sourceDescriptions.checkout.&lt;workflowId&gt;</c> finds this document's workflow by it.
    /// </param>
    /// <param name="document">The Arazzo 1.0.x or 1.1.x document, as JSON or YAML.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    public ArazzoLikeC4Source(string name, string document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(document);
        Name = name;
        Document = document;
    }

    /// <summary>The document's name.</summary>
    public string Name { get; }

    /// <summary>The Arazzo document, as JSON or YAML.</summary>
    public string Document { get; }

    /// <summary>
    /// Where the document is published, such as its URL. Another document's source description with this URL is
    /// this document, in addition to the one named <see cref="Name"/>.
    /// </summary>
    public string? Link { get; init; }
}
