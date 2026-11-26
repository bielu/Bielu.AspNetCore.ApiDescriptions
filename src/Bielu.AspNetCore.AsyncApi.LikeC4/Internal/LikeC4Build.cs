namespace Bielu.AspNetCore.AsyncApi.LikeC4.Internal;

/// <summary>
/// A model being built, with the element and view ids handed out so far, so the AsyncAPI documents and the Arazzo
/// workflows added after them never take each other's ids.
/// </summary>
internal sealed class LikeC4Build
{
    public LikeC4Model Model { get; } = new();

    public LikeC4Identifiers ElementIds { get; } = new();

    public LikeC4Identifiers ViewIds { get; } = new();
}

/// <summary>Where the AsyncAPI documents' services, channels and operations are in the model.</summary>
internal sealed class AsyncApiModelIndex
{
    private readonly List<AsyncApiModelService> _services = [];

    public void Add(AsyncApiModelService service) => _services.Add(service);

    /// <summary>The service whose source has <paramref name="name"/>, ignoring case.</summary>
    public AsyncApiModelService? FindByName(string name) =>
        _services.FirstOrDefault(service => string.Equals(service.Source.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The service whose source links to <paramref name="url"/>.</summary>
    public AsyncApiModelService? FindByLink(string url) =>
        _services.FirstOrDefault(service => string.Equals(service.Source.Link, url, StringComparison.Ordinal));
}

/// <summary>A service element and the channel elements and operations of its document, by their keys.</summary>
internal sealed record AsyncApiModelService(
    AsyncApiLikeC4Source Source,
    string Id,
    IReadOnlyDictionary<string, AsyncApiModelChannel> Channels,
    IReadOnlyDictionary<string, AsyncApiModelOperation> Operations);

/// <summary>A channel element and the messages the document's channel carries.</summary>
internal sealed record AsyncApiModelChannel(string Id, IReadOnlyList<string> Messages);

/// <summary>An operation of a document: what the service does, on which channel element, with which messages.</summary>
internal sealed record AsyncApiModelOperation(AsyncApiOperationAction Action, string ChannelId, IReadOnlyList<string> Messages);
