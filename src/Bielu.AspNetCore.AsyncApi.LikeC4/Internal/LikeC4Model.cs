namespace Bielu.AspNetCore.AsyncApi.LikeC4.Internal;

/// <summary>A LikeC4 workspace file: the kinds it declares, its model and its views.</summary>
internal sealed class LikeC4Model
{
    public List<LikeC4ElementKind> ElementKinds { get; } = [];

    public List<LikeC4RelationshipKind> RelationshipKinds { get; } = [];

    public List<string> Tags { get; } = [];

    public List<LikeC4Element> Elements { get; } = [];

    public List<LikeC4Relationship> Relationships { get; } = [];

    public List<LikeC4View> Views { get; } = [];
}

/// <summary>An element kind of the specification.</summary>
/// <param name="Name">The kind's name.</param>
/// <param name="Notation">How the legend describes it.</param>
/// <param name="Shape">Its shape, or <see langword="null"/> for the default.</param>
internal sealed record LikeC4ElementKind(string Name, string Notation, string? Shape = null);

/// <summary>A relationship kind of the specification.</summary>
/// <param name="Name">The kind's name.</param>
/// <param name="Notation">How the legend describes it.</param>
/// <param name="Line">Its line style (<c>solid</c>, <c>dashed</c>, <c>dotted</c>), or <see langword="null"/> for LikeC4's default, dashed.</param>
internal sealed record LikeC4RelationshipKind(string Name, string Notation, string? Line = null);

/// <summary>A link of an element.</summary>
internal sealed record LikeC4Link(string Url, string? Title);

/// <summary>An element of the model.</summary>
internal sealed record LikeC4Element(string Id, string Kind, string Title)
{
    public string? Description { get; init; }

    public string? Technology { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<LikeC4Link> Links { get; init; } = [];

    /// <summary>Metadata, in the order written.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Metadata { get; init; } = [];
}

/// <summary>A relationship of the model.</summary>
internal sealed record LikeC4Relationship(string Source, string Target, string Kind, string Title)
{
    public string? Description { get; init; }

    public string? Technology { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Metadata, in the order written.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Metadata { get; init; } = [];
}

/// <summary>An element view.</summary>
/// <param name="Id">The view's id.</param>
/// <param name="Title">The view's title.</param>
/// <param name="Of">The element the view is scoped to, or <see langword="null"/> for a view of the whole model.</param>
/// <param name="Include">The predicates the view includes, as LikeC4 writes them.</param>
/// <param name="Exclude">The predicates the view excludes, as LikeC4 writes them.</param>
internal sealed record LikeC4View(
    string Id,
    string Title,
    string? Of,
    IReadOnlyList<string> Include,
    IReadOnlyList<string> Exclude)
{
    public string? Description { get; init; }
}
