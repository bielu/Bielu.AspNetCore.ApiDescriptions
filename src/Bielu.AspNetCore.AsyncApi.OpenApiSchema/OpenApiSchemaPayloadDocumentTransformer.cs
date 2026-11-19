using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.OpenApiSchema;

/// <summary>
/// Replaces the JSON Schema payload of each message with the equivalent OpenAPI 3.0 Schema Object, declared with
/// <c>schemaFormat: application/vnd.oai.openapi;version=3.0.0</c>.
/// </summary>
internal sealed class OpenApiSchemaPayloadDocumentTransformer(OpenApiSchemaPayloadOptions options) : IAsyncApiDocumentTransformer
{
    public Task TransformAsync(AsyncApiDocument document, AsyncApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var converter = new JsonSchemaToOpenApiConverter(document);
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
                SchemaFormat = OpenApiSchemaPayloadOptions.SchemaFormat,
                Schema = new OpenApiSchemaObject(converter.Convert(payload)),
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
