namespace Bielu.AspNetCore.AsyncApi.OpenApiSchema;

/// <summary>Options for describing message payloads as OpenAPI 3.0 Schema Objects.</summary>
/// <example>
/// <code>
/// options.AddOpenApiSchemaPayloads(openApi => openApi.ForMessages("orderPlaced", "orderCancelled"));
/// </code>
/// </example>
public sealed class OpenApiSchemaPayloadOptions
{
    /// <summary>
    /// The <c>schemaFormat</c> declared on each converted payload: the OpenAPI 3.0.0 Schema Object, the only OpenAPI
    /// version the AsyncAPI specification lists.
    /// </summary>
    public const string SchemaFormat = "application/vnd.oai.openapi;version=3.0.0";

    /// <summary>
    /// Decides which messages get an OpenAPI payload, by their key under <c>components/messages</c>. Every message does
    /// when it is <see langword="null"/>, the default.
    /// </summary>
    public Func<string, bool>? MessageFilter { get; set; }

    /// <summary>Limits the OpenAPI payloads to the messages with these keys under <c>components/messages</c>.</summary>
    /// <param name="messageKeys">The message keys, for example message ids.</param>
    /// <returns>The options, for chaining.</returns>
    public OpenApiSchemaPayloadOptions ForMessages(params string[] messageKeys)
    {
        ArgumentNullException.ThrowIfNull(messageKeys);
        foreach (var key in messageKeys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
        }

        var keys = new HashSet<string>(messageKeys, StringComparer.Ordinal);
        MessageFilter = keys.Contains;
        return this;
    }
}
