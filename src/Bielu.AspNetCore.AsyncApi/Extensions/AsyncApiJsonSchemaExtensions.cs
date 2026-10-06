// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;

namespace Bielu.AspNetCore.AsyncApi.Extensions;

internal static class AsyncApiJsonSchemaExtensions
{
    private static readonly AsyncApiJsonSchema _nullSchema = new() { Type = SchemaType.Null };

    public static IAsyncApiSchema CreateOneOfNullableWrapper(this AsyncApiJsonSchema originalSchema)
    {
        return new AsyncApiJsonSchema
        {
            OneOf =
            [
                _nullSchema,
                originalSchema
            ]
        };
    }

    public static bool IsComponentizedSchema(this AsyncApiJsonSchema schema)
        => schema.IsComponentizedSchema(out _);

    public static bool IsComponentizedSchema(this AsyncApiJsonSchema schema, [NotNullWhen(true)] out string? schemaId)
    {
        // if(schema is not null
        //     && schema.Metadata.TryGetValue(AsyncApiConstants.SchemaId, out var schemaIdAsObject)
        //     && schemaIdAsObject is string schemaIdString
        //     && !string.IsNullOrEmpty(schemaIdString))
        // {
        //     schemaId = schemaIdString;
        //     return true;
        // }
        schemaId = null;
        return false;
    }
}
