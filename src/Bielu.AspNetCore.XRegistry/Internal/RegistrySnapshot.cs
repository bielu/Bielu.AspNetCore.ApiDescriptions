using System.Text.Json.Nodes;

namespace Bielu.AspNetCore.XRegistry.Internal;

/// <summary>The registry's content at one point in time: a message group and a schema group per AsyncAPI document.</summary>
/// <param name="Groups">The groups, one per AsyncAPI document.</param>
internal sealed record RegistrySnapshot(IReadOnlyList<RegistryGroup> Groups)
{
    public RegistryGroup? FindGroup(string groupId) =>
        Groups.FirstOrDefault(group => string.Equals(group.Id, groupId, StringComparison.Ordinal));
}

/// <summary>
/// The messages and payload schemas of one AsyncAPI document, served as the message group and the schema group with
/// the same id.
/// </summary>
internal sealed record RegistryGroup(
    string Id,
    string? Name,
    string? Description,
    IReadOnlyList<MessageDefinition> Messages,
    IReadOnlyList<SchemaDefinition> Schemas)
{
    public MessageDefinition? FindMessage(string messageId) =>
        Messages.FirstOrDefault(message => string.Equals(message.Id, messageId, StringComparison.Ordinal));

    public SchemaDefinition? FindSchema(string schemaId) =>
        Schemas.FirstOrDefault(schema => string.Equals(schema.Id, schemaId, StringComparison.Ordinal));
}

/// <summary>A message definition: an AsyncAPI message, pointing at the schema of its payload when it has one.</summary>
internal sealed record MessageDefinition(
    string Id,
    string? Name,
    string? Description,
    string? DataContentType,
    SchemaDefinition? Schema);

/// <summary>A schema: the payload schema of one message, as a standalone document.</summary>
/// <param name="Id">The schema id, the same as its message's.</param>
/// <param name="Name">A human-readable name.</param>
/// <param name="Format">The xRegistry schema format, for example <c>JsonSchema/draft-07</c>.</param>
/// <param name="ContentType">The media type of <see cref="Document"/>.</param>
/// <param name="Document">The schema document, as served.</param>
/// <param name="JsonDocument">The document as JSON, when it is JSON; inlined as <c>schema</c> rather than <c>schemabase64</c>.</param>
internal sealed record SchemaDefinition(
    string Id,
    string? Name,
    string Format,
    string ContentType,
    byte[] Document,
    JsonNode? JsonDocument);
