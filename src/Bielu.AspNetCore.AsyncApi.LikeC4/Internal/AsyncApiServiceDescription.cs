namespace Bielu.AspNetCore.AsyncApi.LikeC4.Internal;

/// <summary>What one AsyncAPI document says about its service: who it is, its channels and its operations.</summary>
/// <param name="Title">The document's <c>info.title</c>.</param>
/// <param name="Version">The document's <c>info.version</c>.</param>
/// <param name="Description">The document's <c>info.description</c>.</param>
/// <param name="Channels">The channels, in document order.</param>
/// <param name="Operations">The operations, in document order.</param>
internal sealed record AsyncApiServiceDescription(
    string? Title,
    string? Version,
    string? Description,
    IReadOnlyList<AsyncApiChannelDescription> Channels,
    IReadOnlyList<AsyncApiOperationDescription> Operations);

/// <summary>A channel of an AsyncAPI document.</summary>
/// <param name="Key">The channel's key in the document (its address, in AsyncAPI 2.x).</param>
/// <param name="Address">The channel's address, or <see langword="null"/> when the document leaves it unknown.</param>
/// <param name="Title">The channel's title.</param>
/// <param name="Description">The channel's description.</param>
/// <param name="Protocols">The protocols of the servers the channel is on, distinct and sorted.</param>
/// <param name="Messages">The names of the messages the channel carries.</param>
internal sealed record AsyncApiChannelDescription(
    string Key,
    string? Address,
    string? Title,
    string? Description,
    IReadOnlyList<string> Protocols,
    IReadOnlyList<string> Messages);

/// <summary>Whether the application sends or receives an operation's messages.</summary>
internal enum AsyncApiOperationAction
{
    /// <summary>The application sends the messages to the channel.</summary>
    Send,

    /// <summary>The application receives the messages from the channel.</summary>
    Receive,
}

/// <summary>An operation of an AsyncAPI document, from the application's point of view.</summary>
/// <param name="Key">The operation's key (its <c>operationId</c>, in AsyncAPI 2.x).</param>
/// <param name="Action">Whether the application sends or receives.</param>
/// <param name="ChannelKey">The <see cref="AsyncApiChannelDescription.Key"/> of the operation's channel.</param>
/// <param name="Summary">The operation's summary, or its title when it has none.</param>
/// <param name="Messages">The names of the messages the operation sends or receives.</param>
internal sealed record AsyncApiOperationDescription(
    string Key,
    AsyncApiOperationAction Action,
    string ChannelKey,
    string? Summary,
    IReadOnlyList<string> Messages);
