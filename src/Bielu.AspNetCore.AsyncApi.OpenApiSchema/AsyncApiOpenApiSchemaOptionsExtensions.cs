using Bielu.AspNetCore.AsyncApi.Services;

namespace Bielu.AspNetCore.AsyncApi.OpenApiSchema;

/// <summary>Describes an AsyncAPI document's message payloads as OpenAPI 3.0 Schema Objects.</summary>
public static class AsyncApiOpenApiSchemaOptionsExtensions
{
    /// <summary>
    /// Replaces the JSON Schema payload of every message (or those <see cref="OpenApiSchemaPayloadOptions.MessageFilter"/>
    /// keeps) with the equivalent OpenAPI 3.0 Schema Object, declared as
    /// <c>schemaFormat: application/vnd.oai.openapi;version=3.0.0</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The OpenAPI 3.0 Schema Object is a dialect of JSON Schema, so most keywords carry over. The ones it spells
    /// differently are rewritten: a type that includes <c>null</c> becomes <c>nullable: true</c>, several types become
    /// an <c>anyOf</c>, a numeric <c>exclusiveMinimum</c>/<c>exclusiveMaximum</c> becomes a bound with a boolean
    /// flag, <c>const</c> becomes a one-value <c>enum</c>, <c>examples</c> becomes <c>example</c>, and a
    /// <c>discriminator</c> property name becomes a Discriminator Object. Keywords OpenAPI 3.0 lacks
    /// (<c>if</c>/<c>then</c>/<c>else</c>, <c>contains</c>, <c>patternProperties</c>, <c>propertyNames</c>,
    /// <c>additionalItems</c>) are dropped.
    /// </para>
    /// <para>
    /// Referenced component schemas are written inline, so the payload is self-contained in the OpenAPI dialect. A
    /// recursive reference stays a <c>$ref</c> to the JSON Schema under <c>components/schemas</c>, which is kept.
    /// Headers are left as JSON Schema. A payload already in another schema format is left alone. The transformer runs
    /// where it is added among the document transformers, so add it after anything that adds messages.
    /// </para>
    /// </remarks>
    /// <param name="options">The options of the document to describe.</param>
    /// <param name="configure">Configures the <see cref="OpenApiSchemaPayloadOptions"/>.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddAsyncApi(options => options
    ///     .AddServer("kafka", "broker:9092", "kafka")
    ///     .AddOpenApiSchemaPayloads());
    /// </code>
    /// </example>
    public static AsyncApiOptions AddOpenApiSchemaPayloads(this AsyncApiOptions options,
        Action<OpenApiSchemaPayloadOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var openApiOptions = new OpenApiSchemaPayloadOptions();
        configure?.Invoke(openApiOptions);
        return options.AddDocumentTransformer(new OpenApiSchemaPayloadDocumentTransformer(openApiOptions));
    }
}
