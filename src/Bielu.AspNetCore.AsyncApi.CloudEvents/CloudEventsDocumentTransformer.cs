using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.CloudEvents;

/// <summary>
/// Describes a document's messages as CloudEvents 1.0: in binary mode it adds the context attributes to each
/// message's headers, in structured mode it wraps each payload in the <c>application/cloudevents+json</c> envelope.
/// </summary>
internal sealed class CloudEventsDocumentTransformer(CloudEventsOptions options) : IAsyncApiDocumentTransformer
{
    internal const string SpecVersion = "1.0";
    internal const string StructuredContentType = "application/cloudevents+json";
    private const string ExampleId = "A234-1234-1234";
    private const string ExampleSource = "/";

    public Task TransformAsync(AsyncApiDocument document, AsyncApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        foreach (var (key, message) in EnumerateMessages(document))
        {
            // A message that is already a structured CloudEvent, such as one the Wolverine integration described,
            // is left as it is.
            if (options.MessageFilter?.Invoke(key) == false || message.ContentType == StructuredContentType)
            {
                continue;
            }

            var messageOptions = options.Messages.GetValueOrDefault(key);
            var attributes = new EventAttributes(
                Type: messageOptions?.Type ?? options.TypePrefix + (message.Name ?? key),
                Source: messageOptions?.Source ?? options.Source,
                DataSchema: messageOptions?.DataSchema);

            if ((messageOptions?.Mode ?? options.Mode) == CloudEventsMode.Structured)
            {
                ApplyStructured(document, message, attributes);
            }
            else
            {
                ApplyBinary(message, attributes, ResolveBinding(document, key, message));
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The messages under <c>components/messages</c>, then those defined inline in a channel, each with the key
    /// it is listed under.
    /// </summary>
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

    private static void ApplyBinary(AsyncApiMessage message, EventAttributes attributes, CloudEventsProtocolBinding binding)
    {
        var prefix = binding switch
        {
            CloudEventsProtocolBinding.Kafka => "ce_",
            CloudEventsProtocolBinding.Amqp => "cloudEvents:",
            CloudEventsProtocolBinding.Mqtt => string.Empty,
            _ => "ce-",
        };

        // datacontenttype is not a header in binary mode: every binding carries it as the protocol's own content type.
        var headers = new AsyncApiJsonSchema
        {
            Type = SchemaType.Object,
            Properties = CreateContextAttributes(attributes, includeDataContentType: false)
                .ToDictionary(attribute => prefix + attribute.Key, attribute => attribute.Value),
            Required = new HashSet<string>(RequiredAttributes.Select(name => prefix + name)),
        };

        if (message.Headers is null)
        {
            message.Headers = new AsyncApiMultiFormatSchema { Schema = headers };
        }
        else if (message.Headers.Schema is AsyncApiJsonSchema existing && IsJsonSchema(message.Headers.SchemaFormat))
        {
            message.Headers = new AsyncApiMultiFormatSchema
            {
                Schema = new AsyncApiJsonSchema { Type = SchemaType.Object, AllOf = [existing, headers] },
            };
        }
        else
        {
            // Headers in another schema format cannot be combined with a JSON Schema.
            return;
        }

        foreach (var example in message.Examples ?? [])
        {
            example.Headers ??= new Dictionary<string, AsyncApiAny>();
            foreach (var (name, value) in CreateExampleAttributes(attributes))
            {
                example.Headers[prefix + name] = new AsyncApiAny(JsonValue.Create(value));
            }
        }
    }

    /// <summary>
    /// Wraps the payload of <paramref name="message"/> in the structured-mode envelope. Returns <see langword="false"/>, leaving
    /// the message unchanged, when its payload is in a schema format other than JSON Schema.
    /// </summary>
    internal static bool ApplyStructured(AsyncApiDocument document, AsyncApiMessage message, EventAttributes attributes)
    {
        if (message.Payload is { } payload && (payload.Schema is not AsyncApiJsonSchema || !IsJsonSchema(payload.SchemaFormat)))
        {
            // The envelope is JSON Schema; a payload in another schema format cannot become its data member.
            return false;
        }

        var dataContentType = attributes.DataContentType ?? message.ContentType ?? document.DefaultContentType ?? "application/json";
        var properties = CreateContextAttributes(attributes, includeDataContentType: true, dataContentType);
        foreach (var (name, description) in attributes.Extensions ?? new Dictionary<string, string>())
        {
            properties[name] = String(description);
        }

        properties["data"] = message.Payload?.Schema as AsyncApiJsonSchema ?? new AsyncApiJsonSchema
        {
            Description = "The event payload.",
        };

        message.Payload = new AsyncApiMultiFormatSchema
        {
            Schema = new AsyncApiJsonSchema
            {
                Type = SchemaType.Object,
                Description = "A CloudEvent in structured mode.",
                Properties = properties,
                Required = new HashSet<string>(RequiredAttributes),
            },
        };
        message.ContentType = StructuredContentType;

        foreach (var example in message.Examples ?? [])
        {
            var envelope = new JsonObject();
            foreach (var (name, value) in CreateExampleAttributes(attributes))
            {
                envelope[name] = value;
            }

            envelope["datacontenttype"] = dataContentType;
            if (example.Payload?.GetNode() is { } data)
            {
                envelope["data"] = data.DeepClone();
            }

            example.Payload = new AsyncApiAny(envelope);
        }

        return true;
    }

    private static readonly string[] RequiredAttributes = ["id", "source", "specversion", "type"];

    private static Dictionary<string, AsyncApiJsonSchema> CreateContextAttributes(EventAttributes attributes,
        bool includeDataContentType, string? dataContentType = null)
    {
        var properties = new Dictionary<string, AsyncApiJsonSchema>(StringComparer.Ordinal)
        {
            ["id"] = String("Identifies the event. Producers ensure that source + id is unique for each distinct event.", minLength: 1),
            ["source"] = String("Identifies the context in which the event happened.", "uri-reference", attributes.Source, minLength: 1),
            ["specversion"] = String("The version of the CloudEvents specification the event uses.", constValue: SpecVersion),
            ["type"] = String("The type of event related to the originating occurrence.", constValue: attributes.Type, minLength: 1),
        };

        if (includeDataContentType)
        {
            properties["datacontenttype"] = String("The content type of data.", constValue: dataContentType);
        }

        properties["dataschema"] = String("Identifies the schema that data adheres to.", "uri", attributes.DataSchema);
        properties["subject"] = String("The subject of the event in the context of the event producer.");
        properties["time"] = String("When the occurrence happened.", "date-time");
        return properties;
    }

    private static IEnumerable<(string Name, string Value)> CreateExampleAttributes(EventAttributes attributes)
    {
        yield return ("id", ExampleId);
        yield return ("source", attributes.Source ?? ExampleSource);
        yield return ("specversion", SpecVersion);
        yield return ("type", attributes.Type);
    }

    private static AsyncApiJsonSchema String(string description, string? format = null, string? constValue = null,
        int? minLength = null) => new()
        {
            Type = SchemaType.String,
            Description = description,
            Format = format,
            Const = constValue is null ? null : new AsyncApiAny(JsonValue.Create(constValue)),
            MinLength = minLength,
        };

    /// <summary>
    /// Picks the binding from the protocols of the servers the message's channels use. A channel without servers is
    /// available on all of them.
    /// </summary>
    private CloudEventsProtocolBinding ResolveBinding(AsyncApiDocument document, string key, AsyncApiMessage message)
    {
        if (options.ProtocolBinding != CloudEventsProtocolBinding.Auto)
        {
            return options.ProtocolBinding;
        }

        var servers = document.Servers ?? new Dictionary<string, AsyncApiServer>();
        var bindings = (document.Channels?.Values ?? [])
            .Where(channel => channel.Messages?.Any(entry => IsMessage(entry, key, message)) == true)
            .SelectMany(channel => channel.Servers is { Count: > 0 }
                ? channel.Servers.Select(server => servers.TryGetValue(ServerKey(server), out var resolved) ? resolved : null)
                : servers.Values)
            .OfType<AsyncApiServer>()
            .Select(server => FromProtocol(server.Protocol))
            .Distinct()
            .ToList();

        return bindings is [var binding] ? binding : CloudEventsProtocolBinding.Http;
    }

    private static bool IsMessage(KeyValuePair<string, AsyncApiMessage> entry, string key, AsyncApiMessage message) =>
        ReferenceEquals(entry.Value, message)
        || entry.Value is AsyncApiMessageReference { Reference.Reference: { } reference }
        && reference.EndsWith("/messages/" + key, StringComparison.Ordinal);

    private static string ServerKey(AsyncApiServer server) =>
        server is AsyncApiServerReference { Reference.Reference: { } reference }
            ? reference[(reference.LastIndexOfAny(['/', '#']) + 1)..]
            : string.Empty;

    internal static CloudEventsProtocolBinding FromProtocol(string? protocol) => protocol?.ToLowerInvariant() switch
    {
        "kafka" or "kafka-secure" => CloudEventsProtocolBinding.Kafka,
        "amqp" or "amqps" => CloudEventsProtocolBinding.Amqp,
        "mqtt" or "mqtts" or "secure-mqtt" or "mqtt5" => CloudEventsProtocolBinding.Mqtt,
        _ => CloudEventsProtocolBinding.Http,
    };

    private static bool IsJsonSchema(string? schemaFormat) =>
        schemaFormat is null
        || schemaFormat.Contains("schema+json", StringComparison.OrdinalIgnoreCase)
        || schemaFormat.Contains("schema+yaml", StringComparison.OrdinalIgnoreCase)
        || schemaFormat.Contains("vnd.aai.asyncapi", StringComparison.OrdinalIgnoreCase);

    /// <summary>The attributes every event of a message carries.</summary>
    /// <param name="Type">The event type, pinned with <c>const</c>.</param>
    /// <param name="Source">The event source, pinned when set.</param>
    /// <param name="DataSchema">The data schema URI, pinned when set.</param>
    /// <param name="DataContentType">The structured-mode <c>datacontenttype</c>; defaults to the message's content type.</param>
    /// <param name="Extensions">Extension attributes of the structured envelope, by name, with their descriptions.</param>
    internal sealed record EventAttributes(string Type, string? Source, string? DataSchema,
        string? DataContentType = null, IReadOnlyDictionary<string, string>? Extensions = null);
}
