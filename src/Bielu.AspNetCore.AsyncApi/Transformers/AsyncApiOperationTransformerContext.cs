// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Services.Schemas;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
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
    /// on the document.
    /// </summary>
    /// <param name="type">The type for which the schema is being created.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, with a value of type <see cref="AsyncApiJsonSchema"/>.</returns>
    public async Task<IAsyncApiSchema> GetOrCreateSchemaAsync(Type type, CancellationToken cancellationToken = default)
    {
        var schemaService = ApplicationServices.GetRequiredKeyedService<AsyncApiJsonSchemaService>(DocumentName);
        return await schemaService.GetOrCreateUnresolvedSchemaAsync(
            document: Document,
            type: type,
            scopedServiceProvider: ApplicationServices,
            schemaTransformers: SchemaTransformers,
            cancellationToken: cancellationToken);
    }
}
