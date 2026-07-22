// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services.XmlDocs;
using Bielu.AspNetCore.AsyncApi.Schemas;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AsyncApiJsonSchema = ByteBard.AsyncAPI.Models.AsyncApiJsonSchema;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Bielu.AspNetCore.AsyncApi.Services.Schemas;

/// <summary>
/// Supports managing elements that belong in the "components" section of
/// an AsyncApi document. In particular, this is the API that is used to
/// interact with the JSON schemas that are managed by a given AsyncApi document.
/// </summary>
[RequiresUnreferencedCode("AsyncApiJsonSchemaService performs reflection for schema generation. This is not AOT compatible, use source generation and provide JsonSerializerContext.")]
[RequiresDynamicCode("AsyncApiJsonSchemaService performs reflection for schema generation. This is not AOT compatible, use source generation and provide JsonSerializerContext.")]
internal sealed class AsyncApiJsonSchemaService
{
    private readonly string _documentName;
    private readonly IOptionsMonitor<AsyncApiOptions> _optionsMonitor;
    private readonly XmlDocumentationProvider _xmlDocumentationProvider;
    private readonly ConcurrentDictionary<Type, string?> _schemaIdCache = new();
    private readonly AsyncApiJsonSchemaContext _jsonSchemaContext;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly JsonSchemaExporterOptions _configuration;

    public AsyncApiJsonSchemaService(
        [ServiceKey] string documentName,
        IOptions<JsonOptions> jsonOptions,
        IOptionsMonitor<AsyncApiOptions> optionsMonitor,
        IServiceProvider serviceProvider)
    {
        _documentName = documentName;
        _optionsMonitor = optionsMonitor;
        _xmlDocumentationProvider = serviceProvider.GetRequiredKeyedService<XmlDocumentationProvider>(documentName);
        _jsonSchemaContext = new AsyncApiJsonSchemaContext(new(jsonOptions.Value.SerializerOptions));
        _jsonSerializerOptions = new JsonSerializerOptions(jsonOptions.Value.SerializerOptions)
        {
            // In order to properly handle the `RequiredAttribute` on type properties, add a modifier to support
            // setting `JsonPropertyInfo.IsRequired` based on the presence of the `RequiredAttribute`.
            TypeInfoResolver = jsonOptions.Value.SerializerOptions.TypeInfoResolver?.WithAddedModifier(jsonTypeInfo =>
            {
                if (jsonTypeInfo.Kind != JsonTypeInfoKind.Object)
                {
                    return;
                }
                foreach (var propertyInfo in jsonTypeInfo.Properties)
                {
                    var hasRequiredAttribute = propertyInfo.AttributeProvider?
                        .GetCustomAttributes(inherit: false)
                        .Any(attr => attr is RequiredAttribute);
                    propertyInfo.IsRequired |= hasRequiredAttribute ?? false;
                }
            })
        };

        _configuration = new JsonSchemaExporterOptions()
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (context, schema) =>
            {
                var type = context.TypeInfo.Type;
                // Fix up schemas generated for IFormFile, IFormFileCollection, Stream, PipeReader and FileContentResult
                // that appear as properties within complex types.
                if (type == typeof(IFormFile) || type == typeof(Stream) || type == typeof(PipeReader) || type == typeof(FileContentResult))
                {
                    schema = new JsonObject
                    {
                        [AsyncApiJsonSchemaKeywords.TypeKeyword] = "string",
                        [AsyncApiJsonSchemaKeywords.FormatKeyword] = "binary",
                        [AsyncApiConstants.Id] = "IFormFile"
                    };
                }
                else if (type == typeof(IFormFileCollection))
                {
                    schema = new JsonObject
                    {
                        [AsyncApiJsonSchemaKeywords.TypeKeyword] = "array",
                        [AsyncApiJsonSchemaKeywords.ItemsKeyword] = new JsonObject
                        {
                            [AsyncApiJsonSchemaKeywords.TypeKeyword] = "string",
                            [AsyncApiJsonSchemaKeywords.FormatKeyword] = "binary",
                            [AsyncApiConstants.Id] = "IFormFile"
                        }
                    };
                }
                else if (type.IsJsonPatchDocument())
                {
                    schema = CreateSchemaForJsonPatch();
                }
                // STJ uses `true` in place of an empty object to represent a schema that matches
                // anything (like the `object` type) or types with user-defined converters. We override
                // this default behavior here to match the format expected in AsyncApi v3.
                if (schema.GetValueKind() == JsonValueKind.True)
                {
                    schema = new JsonObject();
                }
                var createSchemaReferenceId = _optionsMonitor.Get(_documentName).CreateSchemaReferenceId;
                schema.ApplyPrimitiveTypesAndFormats(context, createSchemaReferenceId);
                schema.ApplySchemaReferenceId(context, createSchemaReferenceId);
                schema.MapPolymorphismOptionsToDiscriminator(context, createSchemaReferenceId);
                if (context.PropertyInfo is { } jsonPropertyInfo)
                {
                    schema.ApplyNullabilityContextInfo(jsonPropertyInfo);
                }
                if (context.TypeInfo.Type.GetCustomAttributes(inherit: false).OfType<DescriptionAttribute>().LastOrDefault() is { } typeDescriptionAttribute)
                {
                    schema[AsyncApiJsonSchemaKeywords.DescriptionKeyword] = typeDescriptionAttribute.Description;
                }
                else
                {
                    var xmlDoc = _xmlDocumentationProvider.GetDocumentation(context.TypeInfo.Type);
                    if (xmlDoc?.Summary != null)
                    {
                        schema[AsyncApiJsonSchemaKeywords.DescriptionKeyword] = xmlDoc.Summary;
                    }
                }
                if (context.PropertyInfo is { AttributeProvider: { } attributeProvider })
                {
                    var propertyAttributes = attributeProvider.GetCustomAttributes(inherit: false);
                    if (propertyAttributes.OfType<ValidationAttribute>() is { } validationAttributes)
                    {
                        schema.ApplyValidationAttributes(validationAttributes);
                    }
                    if (propertyAttributes.OfType<DefaultValueAttribute>().LastOrDefault() is { } defaultValueAttribute)
                    {
                        schema.ApplyDefaultValue(defaultValueAttribute.Value, context.TypeInfo);
                    }
                    var isInlinedSchema = !schema.WillBeComponentized();
                    var descriptionAttribute = propertyAttributes.OfType<DescriptionAttribute>().LastOrDefault();
                    var description = descriptionAttribute?.Description;

                    if (description == null && context.PropertyInfo.AttributeProvider is MemberInfo member)
                    {
                        description = _xmlDocumentationProvider.GetDocumentation(member)?.Summary;
                    }

                    if (description != null)
                    {
                        if (isInlinedSchema)
                        {
                            schema[AsyncApiJsonSchemaKeywords.DescriptionKeyword] = description;
                        }
                        else
                        {
                            schema[AsyncApiGeneratorConstants.RefDescriptionAnnotation] = description;
                        }
                    }
                }
                schema.PruneNullTypeForComponentizedTypes();
                return schema;
            }
        };
    }

    private static JsonObject CreateSchemaForJsonPatch()
    {
        var addReplaceTest = new JsonObject()
        {
            [AsyncApiJsonSchemaKeywords.TypeKeyword] = "object",
            [AsyncApiJsonSchemaKeywords.AdditionalPropertiesKeyword] = false,
            [AsyncApiJsonSchemaKeywords.RequiredKeyword] = JsonArray(["op", "path", "value"]),
            [AsyncApiJsonSchemaKeywords.PropertiesKeyword] = new JsonObject
            {
                ["op"] = new JsonObject()
                {
                    [AsyncApiJsonSchemaKeywords.TypeKeyword] = "string",
                    [AsyncApiJsonSchemaKeywords.EnumKeyword] = JsonArray(["add", "replace", "test"]),
                },
                ["path"] = new JsonObject()
                {
                    [AsyncApiJsonSchemaKeywords.TypeKeyword] = "string"
                },
                ["value"] = new JsonObject()
            }
        };

        var moveCopy = new JsonObject()
        {
            [AsyncApiJsonSchemaKeywords.TypeKeyword] = "object",
            [AsyncApiJsonSchemaKeywords.AdditionalPropertiesKeyword] = false,
            [AsyncApiJsonSchemaKeywords.RequiredKeyword] = JsonArray(["op", "path", "from"]),
            [AsyncApiJsonSchemaKeywords.PropertiesKeyword] = new JsonObject
            {
                ["op"] = new JsonObject()
                {
                    [AsyncApiJsonSchemaKeywords.TypeKeyword] = "string",
                    [AsyncApiJsonSchemaKeywords.EnumKeyword] = JsonArray(["move", "copy"]),
                },
                ["path"] = new JsonObject()
                {
                    [AsyncApiJsonSchemaKeywords.TypeKeyword] = "string"
                },
                ["from"] = new JsonObject()
                {
                    [AsyncApiJsonSchemaKeywords.TypeKeyword] = "string"
                },
            }
        };

        var remove = new JsonObject()
        {
            [AsyncApiJsonSchemaKeywords.TypeKeyword] = "object",
            [AsyncApiJsonSchemaKeywords.AdditionalPropertiesKeyword] = false,
            [AsyncApiJsonSchemaKeywords.RequiredKeyword] = JsonArray(["op", "path"]),
            [AsyncApiJsonSchemaKeywords.PropertiesKeyword] = new JsonObject
            {
                ["op"] = new JsonObject()
                {
                    [AsyncApiJsonSchemaKeywords.TypeKeyword] = "string",
                    [AsyncApiJsonSchemaKeywords.EnumKeyword] = JsonArray(["remove"])
                },
                ["path"] = new JsonObject()
                {
                    [AsyncApiJsonSchemaKeywords.TypeKeyword] = "string"
                },
            }
        };

        return new JsonObject
        {
            [AsyncApiConstants.Id] = "JsonPatchDocument",
            [AsyncApiJsonSchemaKeywords.TypeKeyword] = "array",
            [AsyncApiJsonSchemaKeywords.ItemsKeyword] = new JsonObject
            {
                [AsyncApiJsonSchemaKeywords.OneOfKeyword] = JsonArray([addReplaceTest, moveCopy, remove])
            },
        };

        // Using JsonArray inline causes the compile to pick the generic Add<T>() overload
        // which then generates native AoT warnings without adding a cost. To Avoid that use
        // this helper method that uses JsonNode to pick the native AoT compatible overload instead.
        static JsonArray JsonArray(ReadOnlySpan<JsonNode> values)
        {
            var array = new JsonArray();

            foreach (var value in values)
            {
                array.Add(value);
            }

            return array;
        }
    }

 internal async Task<AsyncApiJsonSchema> GetOrCreateUnresolvedSchemaAsync(AsyncApiDocument? document, Type type, IServiceProvider scopedServiceProvider, IAsyncApiSchemaTransformer[] schemaTransformers, ApiParameterDescription? parameterDescription = null, CancellationToken cancellationToken = default)
{
    var schemaAsJsonObject = CreateSchema(type);

    // Convert string type values to numeric enum format and remove null IDs
    ConvertTypeStringsToEnumValues(schemaAsJsonObject);
    RemoveNullIds(schemaAsJsonObject);

    if (parameterDescription is not null)
    {
        schemaAsJsonObject.ApplyParameterInfo(parameterDescription, _jsonSerializerOptions.GetTypeInfo(type));
    }

    try
    {
        var deserializedSchema = JsonSerializer.Deserialize(schemaAsJsonObject, _jsonSchemaContext.AsyncApiJsonSchema);
        Debug.Assert(deserializedSchema != null, "The schema should have been deserialized successfully and materialize a non-null value.");
        await ApplySchemaTransformersAsync(document, deserializedSchema, type, scopedServiceProvider, schemaTransformers, parameterDescription, cancellationToken);
        return deserializedSchema;
    }
    catch (JsonException ex)
    {
        var schemaJson = schemaAsJsonObject.ToJsonString();
        throw new InvalidOperationException(
            $"Failed to deserialize schema for type '{type.FullName}'. Schema: {schemaJson}", ex);
    }
}

private static void ConvertTypeStringsToEnumValues(JsonNode? node)
{
    if (node is JsonObject jsonObject)
    {
        if (jsonObject.ContainsKey("type") && jsonObject["type"] is JsonValue typeValue)
        {
            if (typeValue.GetValueKind() == JsonValueKind.String)
            {
                var typeString = typeValue.GetValue<string>()?.ToLowerInvariant();
                var enumValue = typeString switch
                {
                    "string" => (int)SchemaType.String,    // 32
                    "number" => (int)SchemaType.Number,    // 16
                    "integer" => (int)SchemaType.Integer,  // 64
                    "boolean" => (int)SchemaType.Boolean,  // 2
                    "object" => (int)SchemaType.Object,    // 8
                    "array" => (int)SchemaType.Array,      // 4
                    "null" => (int)SchemaType.Null,        // 1
                    _ => -1
                };
                
                if (enumValue >= 0)
                {
                    jsonObject["type"] = enumValue;
                }
            }
        }

        var keys = ((IDictionary<string, JsonNode?>)jsonObject).Keys.ToList();
        foreach (var key in keys)
        {
            ConvertTypeStringsToEnumValues(jsonObject[key]);
        }
    }
    else if (node is JsonArray jsonArray)
    {
        for (var i = 0; i < jsonArray.Count; i++)
        {
            ConvertTypeStringsToEnumValues(jsonArray[i]);
        }
    }
}

private static void RemoveNullIds(JsonNode? node)
{
    if (node is JsonObject jsonObject)
    {
        if (jsonObject.ContainsKey(AsyncApiConstants.Id) && jsonObject[AsyncApiConstants.Id]?.GetValueKind() == JsonValueKind.Null)
        {
            jsonObject.Remove(AsyncApiConstants.Id);
        }

        var keys = ((IDictionary<string, JsonNode?>)jsonObject).Keys.ToList();
        foreach (var key in keys)
        {
            RemoveNullIds(jsonObject[key]);
        }
    }
    else if (node is JsonArray jsonArray)
    {
        for (var i = 0; i < jsonArray.Count; i++)
        {
            RemoveNullIds(jsonArray[i]);
        }
    }
}

    internal async Task<IAsyncApiSchema> GetOrCreateSchemaAsync(AsyncApiDocument document, Type type, IServiceProvider scopedServiceProvider, IAsyncApiSchemaTransformer[] schemaTransformers, ApiParameterDescription? parameterDescription = null, CancellationToken cancellationToken = default)
    {
        var schema = await GetOrCreateUnresolvedSchemaAsync(document, type, scopedServiceProvider, schemaTransformers, parameterDescription, cancellationToken);

        // Cache the root schema IDs since we expect to be called
        // on the same type multiple times within an API
        var baseSchemaId = _schemaIdCache.GetOrAdd(type, t =>
        {
            var jsonTypeInfo = _jsonSerializerOptions.GetTypeInfo(t);
            return _optionsMonitor.Get(_documentName).CreateSchemaReferenceId(jsonTypeInfo);
        });

        return ResolveReferenceForSchema(document, schema, baseSchemaId);
    }

    internal static AsyncApiJsonSchema ResolveReferenceForSchema(AsyncApiDocument document, IAsyncApiSchema inputSchema, string? rootSchemaId, string? baseSchemaId = null)
    {
        var schema = UnwrapAsyncApiJsonSchema(inputSchema);

        var isComponentizedSchema = schema.IsComponentizedSchema(out var schemaId);

        // When we register it, this will be the resulting reference
        AsyncApiJsonSchemaReference? resultSchemaReference = null;
        if (inputSchema is AsyncApiJsonSchema && isComponentizedSchema)
        {
            var targetReferenceId = baseSchemaId is not null
                ? $"{baseSchemaId}{schemaId}"
                : schemaId;
            if (!string.IsNullOrEmpty(targetReferenceId))
            {
                if (!document.AddAsyncApiJsonSchemaByReference(targetReferenceId, schema, out resultSchemaReference))
                {
                    // We already added this schema, so it has already been resolved.
                    return resultSchemaReference;
                }
            }
        }

        if (schema.AnyOf is { Count: > 0 })
        {
            for (var i = 0; i < schema.AnyOf.Count; i++)
            {
                schema.AnyOf[i] = ResolveReferenceForSchema(document, schema.AnyOf[i], rootSchemaId, schemaId);
            }
        }

        if (schema.Properties is not null)
        {
            foreach (var property in schema.Properties)
            {
                var resolvedProperty = ResolveReferenceForSchema(document, property.Value, rootSchemaId);
                // if (property.Value is AsyncApiJsonSchema targetSchema &&
                //     targetSchema.Metadata?.TryGetValue(AsyncApiConstants.NullableProperty, out var isNullableProperty) == true &&
                //     isNullableProperty is true)
                // {
                //     schema.Properties[property.Key] = resolvedProperty.CreateOneOfNullableWrapper();
                // }
                // else
                // {
                    schema.Properties[property.Key] = resolvedProperty;
                // }
            }
        }

        if (schema.AllOf is { Count: > 0 })
        {
            for (var i = 0; i < schema.AllOf.Count; i++)
            {
                schema.AllOf[i] = ResolveReferenceForSchema(document, schema.AllOf[i], rootSchemaId);
            }
        }

        if (schema.OneOf is { Count: > 0 })
        {
            for (var i = 0; i < schema.OneOf.Count; i++)
            {
                schema.OneOf[i] = ResolveReferenceForSchema(document, schema.OneOf[i], rootSchemaId);
            }
        }

        if (schema.AdditionalProperties is not null)
        {
            schema.AdditionalProperties = ResolveReferenceForSchema(document, schema.AdditionalProperties, rootSchemaId);
        }

        if (schema.Items is not null)
        {
            schema.Items = ResolveReferenceForSchema(document, schema.Items, rootSchemaId);
        }

        if (schema.Not is not null)
        {
            schema.Not = ResolveReferenceForSchema(document, schema.Not, rootSchemaId);
        }

        if (resultSchemaReference is not null)
        {
            return resultSchemaReference;
        }

        return schema;
    }

    private static AsyncApiJsonSchema UnwrapAsyncApiJsonSchema(IAsyncApiSchema sourceSchema)
    {
        if (sourceSchema is AsyncApiJsonSchemaReference schemaReference)
        {
       
                return schemaReference;
           
        }
        else if (sourceSchema is AsyncApiJsonSchema directSchema)
        {
            return directSchema;
        }
        else
        {
            throw new InvalidOperationException($"The input schema must be an {nameof(AsyncApiJsonSchema)} or {nameof(AsyncApiJsonSchemaReference)}.");
        }
    }

    internal async Task ApplySchemaTransformersAsync(AsyncApiDocument? document, AsyncApiJsonSchema schema, Type type, IServiceProvider scopedServiceProvider, IAsyncApiSchemaTransformer[] schemaTransformers, ApiParameterDescription? parameterDescription = null, CancellationToken cancellationToken = default)
    {
        if (schemaTransformers.Length == 0)
        {
            return;
        }
        var jsonTypeInfo = _jsonSerializerOptions.GetTypeInfo(type);
        var context = new AsyncApiJsonSchemaTransformerContext
        {
            DocumentName = _documentName,
            JsonTypeInfo = jsonTypeInfo,
            JsonPropertyInfo = null,
            ParameterDescription = parameterDescription,
            ApplicationServices = scopedServiceProvider,
            Document = document,
            SchemaTransformers = schemaTransformers
        };
        for (var i = 0; i < schemaTransformers.Length; i++)
        {
            // Reset context object to base state before running each transformer.
            var transformer = schemaTransformers[i];
            await InnerApplySchemaTransformersAsync(schema, jsonTypeInfo, null, context, transformer, cancellationToken);
        }
    }

    private async Task InnerApplySchemaTransformersAsync(IAsyncApiSchema inputSchema,
        JsonTypeInfo jsonTypeInfo,
        JsonPropertyInfo? jsonPropertyInfo,
        AsyncApiJsonSchemaTransformerContext context,
        IAsyncApiSchemaTransformer transformer,
        CancellationToken cancellationToken = default)
    {
        context.UpdateJsonTypeInfo(jsonTypeInfo, jsonPropertyInfo);
        var schema = UnwrapAsyncApiJsonSchema(inputSchema);
        await transformer.TransformAsync(schema, context, cancellationToken);

        // Only apply transformers on polymorphic schemas where we can resolve the derived
        // types associated with the base type.
        if (schema.AnyOf is { Count: > 0 } && jsonTypeInfo.PolymorphismOptions is not null)
        {
            var anyOfIndex = 0;
            foreach (var derivedType in jsonTypeInfo.PolymorphismOptions.DerivedTypes)
            {
                var derivedJsonTypeInfo = _jsonSerializerOptions.GetTypeInfo(derivedType.DerivedType);
                if (schema.AnyOf.Count <= anyOfIndex)
                {
                    break;
                }
                await InnerApplySchemaTransformersAsync(schema.AnyOf[anyOfIndex], derivedJsonTypeInfo, null, context, transformer, cancellationToken);
                anyOfIndex++;
            }
        }

        // If the schema is an array but uses AnyOf or OneOf then ElementType is null
        if (schema.Items is not null && jsonTypeInfo.ElementType is not null)
        {
            var elementTypeInfo = _jsonSerializerOptions.GetTypeInfo(jsonTypeInfo.ElementType);
            await InnerApplySchemaTransformersAsync(schema.Items, elementTypeInfo, null, context, transformer, cancellationToken);
        }

        if (schema.Properties is { Count: > 0 })
        {
            foreach (var propertyInfo in jsonTypeInfo.Properties)
            {
                if (schema.Properties.TryGetValue(propertyInfo.Name, out var propertySchema))
                {
                    await InnerApplySchemaTransformersAsync(propertySchema, _jsonSerializerOptions.GetTypeInfo(propertyInfo.PropertyType), propertyInfo, context, transformer, cancellationToken);
                }
            }
        }

        if (schema is {  AdditionalProperties: not null } &&
            jsonTypeInfo.ElementType is not null)
        {
            var elementTypeInfo = _jsonSerializerOptions.GetTypeInfo(jsonTypeInfo.ElementType);
            await InnerApplySchemaTransformersAsync(schema.AdditionalProperties, elementTypeInfo, null, context, transformer, cancellationToken);
        }
    }

    private JsonNode CreateSchema(Type type)
    {
        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(_jsonSerializerOptions, type, _configuration);
        NormalizeSchemaTypes(schema);
        return ResolveReferences(schema, schema);
    }

    private static void NormalizeSchemaTypes(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            // Handle type arrays by selecting the primary type
            if (jsonObject.ContainsKey("type") && jsonObject["type"] is JsonArray typeArray)
            {
                var types = typeArray
                    .Select(t => t?.GetValue<string>())
                    .Where(t => t != null)
                    .ToList();

                // Prefer non-null type for nullable fields
                string? primaryType = types.FirstOrDefault(t => t != "null");
                if (primaryType == null)
                {
                    primaryType = types.FirstOrDefault();
                }

                if (primaryType != null)
                {
                    jsonObject["type"] = primaryType;
                    // Remove pattern if we're keeping the main type
                    jsonObject.Remove("pattern");
                }
            }

            var keys = ((IDictionary<string, JsonNode?>)jsonObject).Keys.ToList();
            foreach (var key in keys)
            {
                NormalizeSchemaTypes(jsonObject[key]);
            }
        }
        else if (node is JsonArray jsonArray)
        {
            for (var i = 0; i < jsonArray.Count; i++)
            {
                NormalizeSchemaTypes(jsonArray[i]);
            }
        }
    }

    private static JsonNode ResolveReferences(JsonNode node, JsonNode rootSchema)
    {
        return ResolveReferencesRecursive(node, rootSchema);
    }

    private static JsonNode ResolveReferencesRecursive(JsonNode node, JsonNode rootSchema)
    {
        if (node is JsonObject jsonObject)
        {
            if (jsonObject.TryGetPropertyValue(AsyncApiConstants.DollarRef, out var refNode) &&
                refNode is JsonValue refValue &&
                refValue.TryGetValue<string>(out var refString) &&
                refString.StartsWith(AsyncApiGeneratorConstants.RefPrefix, StringComparison.Ordinal))
            {
                try
                {
                    // Resolve the reference path to the actual schema content
                    // to avoid relative references
                    var resolvedNode = ResolveReference(refString, rootSchema);
                    if (resolvedNode != null)
                    {
                        return resolvedNode.DeepClone();
                    }
                }
                catch (InvalidOperationException)
                {
                    // If resolution fails due to invalid path, return the original reference
                    // This maintains backward compatibility while preventing crashes
                }

                // If resolution fails, return the original reference
                return node;
            }

            // Process all properties recursively
            var newObject = new JsonObject();
            foreach (var property in jsonObject)
            {
                if (property.Value != null)
                {
                    var processedValue = ResolveReferencesRecursive(property.Value, rootSchema);
                    newObject[property.Key] = processedValue?.DeepClone();
                }
                else
                {
                    newObject[property.Key] = null;
                }
            }
            return newObject;
        }
        else if (node is JsonArray jsonArray)
        {
            var newArray = new JsonArray();
            for (var i = 0; i < jsonArray.Count; i++)
            {
                if (jsonArray[i] != null)
                {
                    var processedValue = ResolveReferencesRecursive(jsonArray[i]!, rootSchema);
                    newArray.Add(processedValue?.DeepClone());
                }
                else
                {
                    newArray.Add(null);
                }
            }
            return newArray;
        }

        // Return non-$ref nodes as-is
        return node;
    }

    private static JsonNode? ResolveReference(string refPath, JsonNode rootSchema)
    {
        if (string.IsNullOrWhiteSpace(refPath))
        {
            throw new InvalidOperationException("Reference path cannot be null or empty.");
        }

        if (!refPath.StartsWith(AsyncApiGeneratorConstants.RefPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Only fragment references (starting with '{AsyncApiGeneratorConstants.RefPrefix}') are supported. Found: {refPath}");
        }

        var path = refPath.TrimStart('#', '/');
        if (string.IsNullOrEmpty(path))
        {
            return rootSchema;
        }

        var segments = path.Split('/');
        var current = rootSchema;

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (current is JsonObject currentObject)
            {
                if (currentObject.TryGetPropertyValue(segment, out var nextNode) && nextNode != null)
                {
                    current = nextNode;
                }
                else
                {
                    var partialPath = string.Join('/', segments.Take(i + 1));
                    throw new InvalidOperationException($"Failed to resolve reference '{refPath}': path segment '{segment}' not found at '#{partialPath}'");
                }
            }
            else
            {
                var partialPath = string.Join('/', segments.Take(i));
                throw new InvalidOperationException($"Failed to resolve reference '{refPath}': cannot navigate beyond '#{partialPath}' - expected object but found {current?.GetType().Name ?? "null"}");
            }
        }

        return current;
    }
}
