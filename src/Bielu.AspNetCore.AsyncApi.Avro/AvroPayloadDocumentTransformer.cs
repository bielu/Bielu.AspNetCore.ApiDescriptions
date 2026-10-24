using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Avro;

/// <summary>
/// Replaces the JSON Schema payload of each message with the equivalent Avro schema, declared with
/// <c>schemaFormat: application/vnd.apache.avro;version=…</c>.
/// </summary>
internal sealed class AvroPayloadDocumentTransformer(AvroPayloadOptions options) : IAsyncApiDocumentTransformer
{
    internal const string SchemaFormatPrefix = "application/vnd.apache.avro;version=";

    public Task TransformAsync(AsyncApiDocument document, AsyncApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        foreach (var (key, message) in EnumerateMessages(document))
        {
            if (options.MessageFilter?.Invoke(key) == false
                || message.Payload is not { Schema: AsyncApiJsonSchema payload } multiFormat
                || !IsJsonSchema(multiFormat.SchemaFormat))
            {
                continue;
            }

            message.Payload = new AsyncApiMultiFormatSchema
            {
                SchemaFormat = SchemaFormatPrefix + options.AvroVersion,
                Schema = new JsonSchemaToAvroConverter(document, options.Namespace).Convert(payload, message.Name ?? key),
            };
        }

        return Task.CompletedTask;
    }

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

    private static bool IsJsonSchema(string? schemaFormat) =>
        schemaFormat is null
        || schemaFormat.Contains("schema+json", StringComparison.OrdinalIgnoreCase)
        || schemaFormat.Contains("schema+yaml", StringComparison.OrdinalIgnoreCase)
        || schemaFormat.Contains("vnd.aai.asyncapi", StringComparison.OrdinalIgnoreCase);
}
