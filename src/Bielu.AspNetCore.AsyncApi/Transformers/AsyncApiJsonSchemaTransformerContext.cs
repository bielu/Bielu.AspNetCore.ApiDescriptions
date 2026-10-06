// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization.Metadata;
using Bielu.AspNetCore.AsyncApi.Services.Schemas;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.AsyncApi.Transformers;

/// <summary>
/// Represents the context in which an AsyncApi schema transformer is executed.
/// </summary>
public sealed class AsyncApiJsonSchemaTransformerContext
{
    private JsonTypeInfo? _jsonTypeInfo;
    private JsonPropertyInfo? _jsonPropertyInfo;

    /// <summary>
    /// Gets the name of the associated AsyncApi document.
    /// </summary>
    public required string DocumentName { get; init; }

    /// <summary>
    /// Gets the <see cref="ApiParameterDescription"/> associated with the target schema.
    /// Null when processing an AsyncApi schema for a response type.
    /// </summary>
    public required ApiParameterDescription? ParameterDescription { get; init; }

    /// <summary>
    /// Gets the <see cref="JsonTypeInfo"/> associated with the target schema.
    /// </summary>
    public required JsonTypeInfo JsonTypeInfo { get => _jsonTypeInfo!; init => _jsonTypeInfo = value; }

    /// <summary>
    /// Gets the <see cref="JsonPropertyInfo"/> associated with the target schema if the
    /// target schema is a property of a parent schema.
    /// </summary>
    public required JsonPropertyInfo? JsonPropertyInfo { get => _jsonPropertyInfo; init => _jsonPropertyInfo = value; }

    /// <summary>
    /// Gets the application services associated with the current document the target schema is in.
    /// </summary>
    public required IServiceProvider ApplicationServices { get; init; }

    /// <summary>
    /// Gets the AsyncApi document the current schema belongs to.
    /// </summary>
    public AsyncApiDocument? Document { get; init; }

    // Expose internal setters for the properties that only allow initializations to avoid allocating
    // new instances of the context for each sub-schema transformation.
    internal void UpdateJsonTypeInfo(JsonTypeInfo jsonTypeInfo, JsonPropertyInfo? jsonPropertyInfo)
    {
        _jsonTypeInfo = jsonTypeInfo;
        _jsonPropertyInfo = jsonPropertyInfo;
    }

    internal IAsyncApiSchemaTransformer[] SchemaTransformers { get; init; } = [];

    /// <summary>
    /// Gets or creates an <see cref="AsyncApiJsonSchema"/> for the specified type. Augments
    /// the schema with any <see cref="IAsyncApiSchemaTransformer"/>s that are registered
    /// on the document. If <paramref name="parameterDescription"/> is not null, the schema will be
    /// augmented with the <see cref="ApiParameterDescription"/> information.
    /// </summary>
    /// <param name="type">The type for which the schema is being created.</param>
    /// <param name="parameterDescription">An optional parameter description to augment the schema.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, with a value of type <see cref="AsyncApiJsonSchema"/>.</returns>
    public Task<AsyncApiJsonSchema> GetOrCreateSchemaAsync(Type type, ApiParameterDescription? parameterDescription = null, CancellationToken cancellationToken = default)
    {
        var schemaService = ApplicationServices.GetRequiredKeyedService<AsyncApiJsonSchemaService>(DocumentName);
        return schemaService.GetOrCreateUnresolvedSchemaAsync(
            document: Document,
            type: type,
            parameterDescription: parameterDescription,
            scopedServiceProvider: ApplicationServices,
            schemaTransformers: SchemaTransformers,
            cancellationToken: cancellationToken);
    }
}
