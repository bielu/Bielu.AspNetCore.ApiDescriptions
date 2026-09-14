// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Services.Schemas;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.AsyncApi.Transformers;

/// <summary>
/// Represents the context in which an AsyncApi operation transformer is executed.
/// </summary>
public sealed class AsyncApiOperationTransformerContext
{
    /// <summary>
    /// Gets the name of the associated AsyncApi document.
    /// </summary>
    public required string DocumentName { get; init; }

    /// <summary>
    /// Gets the API description associated with target operation, when the operation was discovered
    /// from an ASP.NET Core endpoint. Operations discovered from <c>[AsyncApi]</c> attribute metadata
    /// (channels, messages, and operations declared on plain types) have no corresponding
    /// <see cref="ApiDescription"/> and leave this <see langword="null"/>.
    /// </summary>
    public required ApiDescription? Description { get; init; }

    /// <summary>
    /// Gets the application services associated with the current document the target operation is in.
    /// </summary>
    public required IServiceProvider ApplicationServices { get; init; }

    /// <summary>
    /// Gets the AsyncApi document the current endpoint belongs to.
    /// </summary>
    public AsyncApiDocument? Document { get; init; }

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
    public async Task<IAsyncApiSchema> GetOrCreateSchemaAsync(Type type, ApiParameterDescription? parameterDescription = null, CancellationToken cancellationToken = default)
    {
        var schemaService = ApplicationServices.GetRequiredKeyedService<AsyncApiJsonSchemaService>(DocumentName);
        return await schemaService.GetOrCreateUnresolvedSchemaAsync(
            document: Document,
            type: type,
            parameterDescription: parameterDescription,
            scopedServiceProvider: ApplicationServices,
            schemaTransformers: SchemaTransformers,
            cancellationToken: cancellationToken);
    }
}
