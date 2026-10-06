// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;

namespace Bielu.AspNetCore.AsyncApi.Extensions;

internal static class AsyncApiDocumentExtensions
{
    /// <summary>
    /// Registers a <see cref="IAsyncApiSchema" /> into the top-level components store on the
    /// <see cref="AsyncApiDocument" /> and returns a resolvable reference to it.
    /// </summary>
    /// <param name="document">The <see cref="AsyncApiDocument"/> to register the schema onto.</param>
    /// <param name="schemaId">The ID that serves as the key for the schema in the schema store.</param>
    /// <param name="schema">The <see cref="IAsyncApiSchema" /> to register into the document.</param>
    /// <param name="schemaReference">An <see cref="IAsyncApiSchema"/> with a reference to the stored schema.</param>
    /// <returns>Whether the schema was added or already existed.</returns>
    public static bool AddAsyncApiJsonSchemaByReference(this AsyncApiDocument document, string schemaId, AsyncApiMultiFormatSchema schema, out AsyncApiJsonSchemaReference schemaReference)
    {
        var schemaAdded = !document.Components.Schemas.ContainsKey(schemaId);

        if (schemaAdded)
        {
            document.Components.Schemas.Add(schemaId, schema);
        }
        object? description = null;
        object? example = null;
        object? defaultAnnotation = null;

        schemaReference = new AsyncApiJsonSchemaReference(schemaId)
        {
            Description = description as string,
            Examples = example is AsyncApiAny exampleJson ? [exampleJson] : null,
            Default = defaultAnnotation as AsyncApiAny,
        };

        return schemaAdded;
    }
}
