using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI.Models;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Bielu.AspNetCore.AsyncApi.Protobuf;

/// <summary>
/// Replaces the payload of each message whose type is a Protocol Buffers message with its <c>.proto</c> source,
/// declared as <c>schemaFormat: application/vnd.google.protobuf;version=…</c>.
/// </summary>
/// <remarks>
/// A document only names a payload's schema, not its .NET type, so this works in two steps. As a schema transformer it
/// marks the JSON Schema of every <see cref="IMessage"/> type with the message's full name; as a document transformer
/// it then replaces each payload whose schema carries the mark, and removes the marks. A message the
/// <see cref="ProtoSchemaWriter"/> can't render faithfully keeps its JSON Schema.
/// </remarks>
internal sealed class ProtobufPayloadTransformer(ProtobufPayloadOptions options)
    : IAsyncApiSchemaTransformer, IAsyncApiDocumentTransformer
{
    internal const string MarkerExtension = "x-bielu-protobuf-message";
    internal const string SchemaFormatPrefix = "application/vnd.google.protobuf;version=";
    private const string SchemaReferencePrefix = "#/components/schemas/";

    private readonly ConcurrentDictionary<string, MessageDescriptor> _descriptors = new(StringComparer.Ordinal);

    public Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        // A Protocol Buffers message has a parameterless constructor, and its instance Descriptor needs no reflection.
        if (context.JsonPropertyInfo is null
            && typeof(IMessage).IsAssignableFrom(context.JsonTypeInfo.Type)
            && context.JsonTypeInfo.CreateObject?.Invoke() is IMessage message)
        {
            _descriptors.TryAdd(message.Descriptor.FullName, message.Descriptor);
            schema.Extensions[MarkerExtension] = new AsyncApiAny(JsonValue.Create(message.Descriptor.FullName));
        }

        return Task.CompletedTask;
    }

    public Task TransformAsync(AsyncApiDocument document, AsyncApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        foreach (var (key, message) in EnumerateMessages(document))
        {
            if (options.MessageFilter?.Invoke(key) != false
                && Resolve(document, message.Payload) is { } payload
                && FullName(payload) is { } fullName
                && _descriptors.TryGetValue(fullName, out var descriptor)
                && ProtoSchemaWriter.TryWrite(descriptor, out var source))
            {
                message.Payload = new AsyncApiMultiFormatSchema
                {
                    SchemaFormat = SchemaFormatPrefix + ProtoSchemaWriter.SyntaxVersion(descriptor),
                    Schema = new ProtobufSchema(source),
                };
            }
        }

        // The mark also lands on schemas inlined into others, such as the items of a repeated field.
        var visited = new HashSet<AsyncApiJsonSchema>(ReferenceEqualityComparer.Instance);
        foreach (var schema in document.Components?.Schemas?.Values ?? [])
        {
            RemoveMarks(schema.Schema as AsyncApiJsonSchema, visited);
        }

        foreach (var (_, message) in EnumerateMessages(document))
        {
            RemoveMarks(message.Payload?.Schema as AsyncApiJsonSchema, visited);
            RemoveMarks(message.Headers?.Schema as AsyncApiJsonSchema, visited);
        }

        return Task.CompletedTask;
    }

    private static void RemoveMarks(AsyncApiJsonSchema? schema, HashSet<AsyncApiJsonSchema> visited)
    {
        if (schema is null or AsyncApiJsonSchemaReference || !visited.Add(schema))
        {
            return;
        }

        schema.Extensions?.Remove(MarkerExtension);
        IEnumerable<AsyncApiJsonSchema?> children =
        [
            schema.Items, schema.AdditionalItems, schema.AdditionalProperties, schema.PropertyNames, schema.Not,
            schema.Contains, schema.If, schema.Then, schema.Else,
            .. schema.Properties?.Values ?? [], .. schema.PatternProperties?.Values ?? [],
            .. schema.AllOf ?? [], .. schema.OneOf ?? [], .. schema.AnyOf ?? [],
        ];
        foreach (var child in children)
        {
            RemoveMarks(child, visited);
        }
    }

    private static AsyncApiJsonSchema? Resolve(AsyncApiDocument document, AsyncApiMultiFormatSchema? payload) =>
        payload?.Schema switch
        {
            AsyncApiJsonSchemaReference { Reference.Reference: { } reference }
                when reference.StartsWith(SchemaReferencePrefix, StringComparison.Ordinal)
                => document.Components?.Schemas?.TryGetValue(reference[SchemaReferencePrefix.Length..], out var target) == true
                    ? target.Schema as AsyncApiJsonSchema
                    : null,
            AsyncApiJsonSchemaReference => null,
            AsyncApiJsonSchema inline => inline,
            _ => null,
        };

    private static string? FullName(AsyncApiJsonSchema schema) =>
        schema.Extensions?.TryGetValue(MarkerExtension, out var marker) == true && marker is AsyncApiAny any
                                                                       && any.TryGetValue(out string fullName)
            ? fullName
            : null;

    private static IEnumerable<(string Key, AsyncApiMessage Message)> EnumerateMessages(AsyncApiDocument document)
    {
        foreach (var (key, message) in document.Components?.Messages ?? new Dictionary<string, AsyncApiMessage>())
        {
            if (message is not AsyncApiMessageReference)
            {
                yield return (key, message);
            }
        }

        foreach (var channel in document.Channels?.Values ?? [])
        {
            foreach (var (key, message) in channel.Messages ?? new Dictionary<string, AsyncApiMessage>())
            {
                if (message is not AsyncApiMessageReference)
                {
                    yield return (key, message);
                }
            }
        }
    }
}
