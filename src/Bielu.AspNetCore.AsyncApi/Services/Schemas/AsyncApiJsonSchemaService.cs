// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Helpers;
using Bielu.AspNetCore.AsyncApi.Schemas;
using Bielu.AspNetCore.AsyncApi.Services.XmlDocs;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    // The type that claimed each components/schemas key, per document being generated: shared by the
    // attribute pipeline and document transformers, so neither can silently reuse the other's schema.
    private readonly ConditionalWeakTable<AsyncApiDocument, Dictionary<string, Type>> _schemaKeyOwners = new();
    private readonly AsyncApiJsonSchemaContext _jsonSchemaContext;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    // Written onto every exported object schema and stripped before deserialization; see CreateExporterOptions.
    private const string SchemaTypeMarker = "x-bielu-schema-type-index";

    public AsyncApiJsonSchemaService(
        [ServiceKey] string documentName,
        IOptions<JsonOptions> jsonOptions,
        IOptionsMonitor<AsyncApiOptions> optionsMonitor,
        IServiceProvider serviceProvider)
    {
        _documentName = documentName;
        _optionsMonitor = optionsMonitor;
        _xmlDocumentationProvider = serviceProvider.GetRequiredKeyedService<XmlDocumentationProvider>(documentName);
        // A document-scoped AsyncApiJsonSchemaJsonOptions overrides the app-wide JsonOptions for schema
        // generation only (e.g. to use a different PropertyNamingPolicy for message payload properties),
        // without touching how the app actually serializes payloads at runtime.
        var baseSerializerOptions = optionsMonitor.Get(documentName).AsyncApiJsonSchemaJsonOptions
            ?? jsonOptions.Value.SerializerOptions;
        // _jsonSchemaContext deserializes the raw JsonSchemaExporter output — which always uses fixed,
        // lowercase JSON Schema keywords ("type", "properties", "required", ...) per the spec — into the
        // internal AsyncApiJsonSchema model's PascalCase properties. That mapping relies on the app-wide
        // JsonOptions' camelCase policy and must stay independent of AsyncApiJsonSchemaJsonOptions: honoring
        // the override here (e.g. a user setting PropertyNamingPolicy = null for PascalCase payloads) would
        // break the "type"->Type, "properties"->Properties lookup and silently deserialize every schema as empty.
        var schemaContextOptions = new JsonSerializerOptions(jsonOptions.Value.SerializerOptions);
        // Without this, any exported schema containing AsyncApiAny (an enum's "enum" keyword, a
        // "default", a "const", ...) throws JsonException the moment that property is populated —
        // AsyncApiAny has no parameterless constructor for the default converter to use. See
        // AsyncApiAnyJsonConverter's own remarks.
        schemaContextOptions.Converters.Add(new AsyncApiAnyJsonConverter());
        _jsonSchemaContext = new AsyncApiJsonSchemaContext(schemaContextOptions);
        // Fall back to the default reflection-based resolver when the base options don't carry one
        // (e.g. a bare `new JsonSerializerOptions { PropertyNamingPolicy = ... }` passed via
        // AsyncApiJsonSchemaJsonOptions), so the RequiredAttribute modifier below still has a resolver to attach to.
        var baseTypeInfoResolver = baseSerializerOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
        _jsonSerializerOptions = new JsonSerializerOptions(baseSerializerOptions)
        {
            // In order to properly handle the `RequiredAttribute` on type properties, add a modifier to support
            // setting `JsonPropertyInfo.IsRequired` based on the presence of the `RequiredAttribute`.
            TypeInfoResolver = baseTypeInfoResolver.WithAddedModifier(jsonTypeInfo =>
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
    }

    /// <summary>
    /// The exporter options for one <see cref="CreateSchema"/> call. Each object schema is stamped with the index of
    /// the type it describes in <paramref name="schemaTypes"/>, so a recursive <c>$ref</c> the exporter emits can be
    /// traced back to that type's component. The stamp is removed again before the schema is deserialized.
    /// </summary>
    private JsonSchemaExporterOptions CreateExporterOptions(List<Type> schemaTypes) => new()
    {
        TreatNullObliviousAsNonNullable = true,
        TransformSchemaNode = (context, schema) =>
        {
            schema = TransformSchemaNode(context, schema);
            if (schema is JsonObject schemaObject)
            {
                schemaObject[SchemaTypeMarker] = schemaTypes.Count;
                schemaTypes.Add(context.TypeInfo.Type);
            }

            return schema;
        }
    };

    private JsonNode TransformSchemaNode(JsonSchemaExporterContext context, JsonNode schema)
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
            var descriptionAttribute = propertyAttributes.OfType<DescriptionAttribute>().LastOrDefault();
            var description = descriptionAttribute?.Description;

            if (description == null && context.PropertyInfo.AttributeProvider is MemberInfo member)
            {
                description = _xmlDocumentationProvider.GetDocumentation(member)?.Summary;
            }

            // Property schemas stay inline (only a recursive back-reference becomes a $ref), so the
            // description goes on the schema itself, also for enums and named complex types. It
            // replaces the type's own summary set above, which describes the type, not the property.
            if (description != null)
            {
                schema[AsyncApiJsonSchemaKeywords.DescriptionKeyword] = description;
            }
        }
        // Nullable componentized-object schemas deliberately keep "null" in their type. A former
        // PruneNullTypeForComponentizedTypes step stripped it, assuming a oneOf-wrapping restoration
        // would re-add nullability at the property reference site. That restoration never worked,
        // so pruning silently dropped nullability; see NullablePropertySchemaTests.
        return schema;
    }

    private static JsonObject CreateSchemaForJsonPatch()
    {
        // "additionalProperties": false is written as the equivalent { "not": {} }: the boolean schema form
        // cannot be deserialized into AsyncApiJsonSchema, which used to fail generation of the whole document.
        var addReplaceTest = new JsonObject()
        {
            [AsyncApiJsonSchemaKeywords.TypeKeyword] = "object",
            [AsyncApiJsonSchemaKeywords.AdditionalPropertiesKeyword] = NoAdditionalProperties(),
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
            [AsyncApiJsonSchemaKeywords.AdditionalPropertiesKeyword] = NoAdditionalProperties(),
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
            [AsyncApiJsonSchemaKeywords.AdditionalPropertiesKeyword] = NoAdditionalProperties(),
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
        static JsonObject NoAdditionalProperties() => new() { [AsyncApiJsonSchemaKeywords.NotKeyword] = new JsonObject() };

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

    internal async Task<AsyncApiJsonSchema> GetOrCreateUnresolvedSchemaAsync(AsyncApiDocument? document, Type type, IServiceProvider scopedServiceProvider, IAsyncApiSchemaTransformer[] schemaTransformers, CancellationToken cancellationToken = default)
    {
        var (schemaAsJsonObject, recursiveReferences) = CreateSchema(type);

        // Convert string type values to numeric enum format and remove null IDs
        ConvertTypeStringsToEnumValues(schemaAsJsonObject);
        RemoveNullIds(schemaAsJsonObject);

        try
        {
            var deserializedSchema = JsonSerializer.Deserialize(schemaAsJsonObject, _jsonSchemaContext.AsyncApiJsonSchema);
            Debug.Assert(deserializedSchema != null, "The schema should have been deserialized successfully and materialize a non-null value.");
            LinkRecursiveReferences(document, deserializedSchema, recursiveReferences);
            await ApplySchemaTransformersAsync(document, deserializedSchema, type, scopedServiceProvider, schemaTransformers, cancellationToken);
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
                    var typeString = typeValue.GetValue<string>();
                    if (MapTypeStringToSchemaTypeFlag(typeString) is { } enumValue)
                    {
                        jsonObject["type"] = (int)enumValue;
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

    // SchemaType is a [Flags] enum, so a comma-separated combination like "Null, String" (the
    // default ToString() output for a combined flags value) needs to parse back into the
    // combined value too, not just the single lowercase type names ("string", "null", ...)
    // that come straight from a JSON Schema "type" array/string.
    private static SchemaType? MapTypeStringToSchemaTypeFlag(string? typeString) =>
        !string.IsNullOrEmpty(typeString) && Enum.TryParse<SchemaType>(typeString, ignoreCase: true, out var result)
            ? result
            : null;

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

    internal async Task<IAsyncApiSchema> GetOrCreateSchemaAsync(AsyncApiDocument document, Type type, IServiceProvider scopedServiceProvider, IAsyncApiSchemaTransformer[] schemaTransformers, CancellationToken cancellationToken = default)
    {
        var schema = await GetOrCreateUnresolvedSchemaAsync(document, type, scopedServiceProvider, schemaTransformers, cancellationToken);

        var baseSchemaId = GetSchemaReferenceId(type);

        return ResolveReferenceForSchema(document, schema, baseSchemaId);
    }

    /// <summary>
    /// Resolves the schema reference id that <see cref="AsyncApiOptions.CreateSchemaReferenceId"/> assigns to
    /// <paramref name="type"/>, caching the result since the same type is commonly looked up many times while
    /// generating a single document. This is the single authoritative source for a payload type's reference id:
    /// any caller that needs to key a component store by type (for example <see cref="AsyncApiDocumentService"/>
    /// registering schema/message components) should call this instead of re-deriving an id independently, so
    /// that a custom <see cref="AsyncApiOptions.CreateSchemaReferenceId"/> and the documented "null means inline"
    /// contract are honored consistently everywhere.
    /// </summary>
    /// <param name="type">The payload type to resolve a schema reference id for.</param>
    /// <returns>The reference id to use, or <see langword="null"/> if the schema should always be inlined.</returns>
    internal string? GetSchemaReferenceId(Type type)
    {
        // Cache the root schema IDs since we expect to be called
        // on the same type multiple times within an API
        return _schemaIdCache.GetOrAdd(type, t =>
        {
            var jsonTypeInfo = _jsonSerializerOptions.GetTypeInfo(t);
            return _optionsMonitor.Get(_documentName).CreateSchemaReferenceId(jsonTypeInfo);
        });
    }

    /// <summary>
    /// The <c>components/schemas</c> key for <paramref name="type"/>: its <see cref="GetSchemaReferenceId"/>,
    /// camel-cased and sanitized. Does not record or validate ownership of the key.
    /// </summary>
    /// <returns>The component key, or <see langword="null"/> if the schema should always be inlined.</returns>
    internal string? GetSchemaComponentKey(Type type) =>
        GetSchemaReferenceId(type) switch
        {
            null => null,
            "" => "",
            var referenceId => AsyncApiNamingHelper.SanitizeKey(char.ToLowerInvariant(referenceId[0]) + referenceId[1..]),
        };

    /// <summary>
    /// Resolves (and if necessary creates and registers) the schema component for <paramref name="type"/>,
    /// keyed by <see cref="GetSchemaComponentKey"/> so a custom <see cref="AsyncApiOptions.CreateSchemaReferenceId"/>
    /// is honored. Throws when a <em>different</em> type already claimed the same key for this document, whether
    /// through the attribute pipeline or a document transformer: silently keeping the first registration would
    /// make one payload describe the wrong type.
    /// </summary>
    /// <param name="document">The document whose <c>components/schemas</c> receives the schema.</param>
    /// <param name="type">The payload type.</param>
    /// <param name="scopedServiceProvider">The request-scoped service provider.</param>
    /// <param name="schemaTransformers">The schema transformers to apply.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The schema's component key, or <see langword="null"/> when <see cref="AsyncApiOptions.CreateSchemaReferenceId"/>
    /// opted the type out of componentization (the schema is inlined instead), along with the resolved schema.
    /// </returns>
    internal async Task<(string? SchemaKey, IAsyncApiSchema Schema)> GetOrCreateComponentSchemaAsync(
        AsyncApiDocument document,
        Type type,
        IServiceProvider scopedServiceProvider,
        IAsyncApiSchemaTransformer[] schemaTransformers,
        CancellationToken cancellationToken)
    {
        var schema = await GetOrCreateSchemaAsync(
            document, type, scopedServiceProvider, schemaTransformers, cancellationToken);

        var key = GetSchemaComponentKey(type);
        if (key is null)
        {
            return (null, schema);
        }

        AddComponentSchema(document, key, type, (AsyncApiJsonSchema)schema);

        return (key, schema);
    }

    /// <summary>
    /// Registers <paramref name="schema"/> under <paramref name="key"/> in <c>components/schemas</c> unless the key is
    /// already registered for <paramref name="type"/>. Throws when a different type already claimed the key.
    /// </summary>
    private void AddComponentSchema(AsyncApiDocument document, string key, Type type, AsyncApiJsonSchema schema)
    {
        var schemaKeyOwners = _schemaKeyOwners.GetValue(document, _ => new Dictionary<string, Type>(StringComparer.Ordinal));
        if (!schemaKeyOwners.TryAdd(key, type) && schemaKeyOwners[key] != type)
        {
            throw new InvalidOperationException(
                $"AsyncAPI schema id '{key}' is already used by type '{schemaKeyOwners[key].FullName}' and cannot also " +
                $"represent type '{type.FullName}': both types produced the same schema reference id " +
                "after sanitization. Configure AsyncApiOptions.CreateSchemaReferenceId to assign these types " +
                "distinct, stable ids.");
        }

        document.Components ??= new AsyncApiComponents();
        document.Components.Schemas ??= new Dictionary<string, AsyncApiMultiFormatSchema>();
        document.Components.Schemas.TryAdd(key, new AsyncApiMultiFormatSchema { Schema = schema });
    }

    /// <summary>
    /// Replaces each recursive back-reference with a <c>$ref</c> to the component of the type it recurses into, and
    /// registers that type's schema as the component. The type's first occurrence stays inline, so the property
    /// that holds it keeps its own description, default and nullability.
    /// </summary>
    /// <remarks>
    /// A back-reference always points at an ancestor of itself, so replacing one never moves the schema another
    /// one points at. Without a document there is no <c>components/schemas</c> to point into, so the
    /// back-references stay empty schemas.
    /// </remarks>
    private void LinkRecursiveReferences(
        AsyncApiDocument? document,
        AsyncApiJsonSchema rootSchema,
        List<RecursiveReference> recursiveReferences)
    {
        if (document is null)
        {
            return;
        }

        foreach (var recursiveReference in recursiveReferences)
        {
            var targetType = recursiveReference.TargetType;
            if (GetSchemaComponentKey(targetType) is not { Length: > 0 } key
                || !TryGetSubschema(rootSchema, recursiveReference.TargetPath, out var target))
            {
                continue;
            }

            AddComponentSchema(document, key, targetType, target);
            TryReplaceSubschema(rootSchema, recursiveReference.SourcePath,
                new AsyncApiJsonSchemaReference($"#/components/schemas/{key}"));
        }
    }

    /// <summary>
    /// A place where <paramref name="TargetType"/> recurses into itself: where the back-reference sits, and where the
    /// type's outermost occurrence starts, both as JSON pointer segments from the root schema.
    /// </summary>
    private sealed record RecursiveReference(string[] SourcePath, string[] TargetPath, Type TargetType);

    private static bool TryGetReference(JsonObject schema, [NotNullWhen(true)] out string? reference)
    {
        if (schema.TryGetPropertyValue(AsyncApiConstants.DollarRef, out var refNode)
            && refNode is JsonValue refValue
            && refValue.TryGetValue(out reference)
            && reference.StartsWith(AsyncApiGeneratorConstants.RefPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        reference = null;
        return false;
    }

    private static bool TryGetSubschema(AsyncApiJsonSchema root, string[] path, [NotNullWhen(true)] out AsyncApiJsonSchema? subschema)
    {
        subschema = root;
        for (var i = 0; i < path.Length && subschema is not null; i++)
        {
            subschema = path[i] switch
            {
                AsyncApiJsonSchemaKeywords.PropertiesKeyword when i + 1 < path.Length
                    => subschema.Properties?.TryGetValue(path[++i], out var property) == true ? property : null,
                AsyncApiJsonSchemaKeywords.ItemsKeyword => subschema.Items,
                AsyncApiJsonSchemaKeywords.AdditionalPropertiesKeyword => subschema.AdditionalProperties,
                AsyncApiJsonSchemaKeywords.NotKeyword => subschema.Not,
                AsyncApiJsonSchemaKeywords.AnyOfKeyword when i + 1 < path.Length => ElementAt(subschema.AnyOf, path[++i]),
                AsyncApiJsonSchemaKeywords.OneOfKeyword when i + 1 < path.Length => ElementAt(subschema.OneOf, path[++i]),
                AsyncApiJsonSchemaKeywords.AllOfKeyword when i + 1 < path.Length => ElementAt(subschema.AllOf, path[++i]),
                _ => null,
            };
        }

        return subschema is not null;

        static AsyncApiJsonSchema? ElementAt(IList<AsyncApiJsonSchema>? schemas, string index) =>
            int.TryParse(index, out var i) && schemas is not null && i >= 0 && i < schemas.Count ? schemas[i] : null;
    }

    private static void TryReplaceSubschema(AsyncApiJsonSchema root, string[] path, AsyncApiJsonSchema replacement)
    {
        // The last step is one segment ("items") or a keyword plus a key or index ("properties", "children").
        var keyed = path.Length >= 2 && path[^2] is AsyncApiJsonSchemaKeywords.PropertiesKeyword
            or AsyncApiJsonSchemaKeywords.AnyOfKeyword or AsyncApiJsonSchemaKeywords.OneOfKeyword
            or AsyncApiJsonSchemaKeywords.AllOfKeyword;
        var parentLength = path.Length - (keyed ? 2 : 1);
        if (parentLength < 0 || !TryGetSubschema(root, path[..parentLength], out var parent))
        {
            return;
        }

        switch (path[parentLength])
        {
            case AsyncApiJsonSchemaKeywords.PropertiesKeyword when parent.Properties?.ContainsKey(path[^1]) == true:
                parent.Properties[path[^1]] = replacement;
                break;
            case AsyncApiJsonSchemaKeywords.ItemsKeyword:
                parent.Items = replacement;
                break;
            case AsyncApiJsonSchemaKeywords.AdditionalPropertiesKeyword:
                parent.AdditionalProperties = replacement;
                break;
            case AsyncApiJsonSchemaKeywords.NotKeyword:
                parent.Not = replacement;
                break;
            case AsyncApiJsonSchemaKeywords.AnyOfKeyword:
                SetElement(parent.AnyOf, path[^1], replacement);
                break;
            case AsyncApiJsonSchemaKeywords.OneOfKeyword:
                SetElement(parent.OneOf, path[^1], replacement);
                break;
            case AsyncApiJsonSchemaKeywords.AllOfKeyword:
                SetElement(parent.AllOf, path[^1], replacement);
                break;
        }

        static void SetElement(IList<AsyncApiJsonSchema>? schemas, string index, AsyncApiJsonSchema value)
        {
            if (int.TryParse(index, out var i) && schemas is not null && i >= 0 && i < schemas.Count)
            {
                schemas[i] = value;
            }
        }
    }

    internal static AsyncApiJsonSchema ResolveReferenceForSchema(AsyncApiDocument document, IAsyncApiSchema inputSchema, string? rootSchemaId, string? baseSchemaId = null)
    {
        // Already a reference (a recursive back-reference); walking into it would cycle back to its target.
        if (inputSchema is AsyncApiJsonSchemaReference existingReference)
        {
            return existingReference;
        }

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

    internal async Task ApplySchemaTransformersAsync(AsyncApiDocument? document, AsyncApiJsonSchema schema, Type type, IServiceProvider scopedServiceProvider, IAsyncApiSchemaTransformer[] schemaTransformers, CancellationToken cancellationToken = default)
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
        // A recursive back-reference: the schema it points at is transformed where it is defined.
        if (inputSchema is AsyncApiJsonSchemaReference)
        {
            return;
        }

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
#if NET11_0_OR_GREATER
        // A C# union (or [Union] type) exports as an anyOf with one entry per case, in declaration order
        // and without a discriminator, so the case types come from the union contract rather than
        // PolymorphismOptions. Only walk it when the entries line up one-to-one with the cases.
        else if (schema.AnyOf is { Count: > 0 } && GetUnionTypeInfo(jsonTypeInfo) is { } unionTypeInfo
                 && unionTypeInfo.UnionCases.Count == schema.AnyOf.Count)
        {
            for (var i = 0; i < unionTypeInfo.UnionCases.Count; i++)
            {
                var caseJsonTypeInfo = _jsonSerializerOptions.GetTypeInfo(unionTypeInfo.UnionCases[i].CaseType);
                await InnerApplySchemaTransformersAsync(schema.AnyOf[i], caseJsonTypeInfo, null, context, transformer, cancellationToken);
            }
        }
#endif

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

        if (schema is { AdditionalProperties: not null } &&
            jsonTypeInfo.ElementType is not null)
        {
            var elementTypeInfo = _jsonSerializerOptions.GetTypeInfo(jsonTypeInfo.ElementType);
            await InnerApplySchemaTransformersAsync(schema.AdditionalProperties, elementTypeInfo, null, context, transformer, cancellationToken);
        }
    }

#if NET11_0_OR_GREATER
    /// <summary>
    /// The union contract behind <paramref name="jsonTypeInfo"/>: the type itself, or the union a
    /// <see cref="Nullable{T}"/> wraps (unions declared with <c>union</c> are structs).
    /// </summary>
    private JsonTypeInfo? GetUnionTypeInfo(JsonTypeInfo jsonTypeInfo)
    {
        if (jsonTypeInfo.Kind == JsonTypeInfoKind.Union)
        {
            return jsonTypeInfo;
        }

        return Nullable.GetUnderlyingType(jsonTypeInfo.Type) is { } underlyingType
            && _jsonSerializerOptions.GetTypeInfo(underlyingType) is { Kind: JsonTypeInfoKind.Union } underlyingTypeInfo
                ? underlyingTypeInfo
                : null;
    }
#endif

    private (JsonNode Schema, List<RecursiveReference> RecursiveReferences) CreateSchema(Type type)
    {
        try
        {
            var schemaTypes = new List<Type>();
            var schema = JsonSchemaExporter.GetJsonSchemaAsNode(_jsonSerializerOptions, type, CreateExporterOptions(schemaTypes));
            NormalizeSchemaTypes(schema);
            var recursiveReferences = new List<RecursiveReference>();
            var resolved = ResolveReferencesRecursive(schema, schema, [], [], recursiveReferences, schemaTypes);
            RemoveSchemaTypeMarkers(resolved);
            return (resolved, recursiveReferences);
        }
        catch (NotSupportedException ex) when (ex.Message.Contains("JsonTypeInfo"))
        {
            throw new InvalidOperationException(
                $"JSON metadata for type '{type.FullName}' was not found. When using Native AOT, ensure that all message types are registered in a JsonSerializerContext and added to the TypeInfoResolverChain of your JsonOptions.",
                ex);
        }
    }

    private static void NormalizeSchemaTypes(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            // SchemaType is a [Flags] enum, so a type array like ["string", "null"] is combined
            // into a single bitwise-OR'd value (e.g. String | Null) instead of picking one entry
            // and discarding the rest, which would silently drop nullability from the schema.
            if (jsonObject.ContainsKey("type") && jsonObject["type"] is JsonArray typeArray)
            {
                SchemaType? combinedType = null;
                foreach (var typeNode in typeArray)
                {
                    if (MapTypeStringToSchemaTypeFlag(typeNode?.GetValue<string>()) is { } flag)
                    {
                        combinedType = combinedType is { } existing ? existing | flag : flag;
                    }
                }

                if (combinedType is { } resolvedType)
                {
                    jsonObject["type"] = (int)resolvedType;
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

    /// <summary>
    /// Copies <paramref name="node"/>, inlining every <c>$ref</c> the schema exporter emits. The exporter uses them
    /// to point at the first occurrence of a type it has already described, which is how it writes recursion down.
    /// Inlining stops where a type reappears below itself: that back-reference becomes an empty schema here and is
    /// recorded in <paramref name="recursiveReferences"/> against the type's outermost occurrence, so
    /// <see cref="LinkRecursiveReferences"/> can turn it into a <c>$ref</c> to the type's component.
    /// </summary>
    /// <param name="node">The node to copy.</param>
    /// <param name="rootSchema">The exporter's root schema, which the <c>$ref</c> pointers are relative to.</param>
    /// <param name="path">The JSON pointer segments of <paramref name="node"/>'s position in the copy.</param>
    /// <param name="ancestors">The types being described around <paramref name="node"/>, with where each one starts.</param>
    /// <param name="recursiveReferences">Receives the back-references found.</param>
    /// <param name="schemaTypes">The types the exporter's schema type markers index into.</param>
    private JsonNode ResolveReferencesRecursive(
        JsonNode node,
        JsonNode rootSchema,
        List<string> path,
        List<(Type Type, string[] Path)> ancestors,
        List<RecursiveReference> recursiveReferences,
        List<Type> schemaTypes)
    {
        if (node is JsonObject jsonObject)
        {
            // A pointer can land on another pointer; the hop limit only guards against a malformed chain.
            for (var hops = 0; hops < 8 && TryGetReference(jsonObject, out var refString); hops++)
            {
                JsonNode? resolvedNode;
                try
                {
                    resolvedNode = ResolveReference(refString, rootSchema);
                }
                catch (InvalidOperationException)
                {
                    // An unresolvable pointer is kept as written rather than failing the whole document.
                    return jsonObject.DeepClone();
                }

                if (resolvedNode is not JsonObject resolvedObject)
                {
                    return resolvedNode?.DeepClone() ?? jsonObject.DeepClone();
                }

                jsonObject = resolvedObject;
            }

            var pushed = false;
            if (jsonObject[SchemaTypeMarker] is JsonValue marker && marker.TryGetValue<int>(out var typeIndex))
            {
                var type = Nullable.GetUnderlyingType(schemaTypes[typeIndex]) ?? schemaTypes[typeIndex];
                var ancestor = ancestors.FindIndex(candidate => candidate.Type == type);
                if (ancestor >= 0)
                {
                    // A type that is always inlined has no component to point to, so its recursion ends here.
                    if (GetSchemaComponentKey(type) is { Length: > 0 })
                    {
                        recursiveReferences.Add(new RecursiveReference([.. path], ancestors[ancestor].Path, type));
                    }

                    return new JsonObject();
                }

                ancestors.Add((type, [.. path]));
                pushed = true;
            }

            var newObject = new JsonObject();
            foreach (var property in jsonObject)
            {
                if (property.Value != null)
                {
                    path.Add(property.Key);
                    var processedValue = ResolveReferencesRecursive(property.Value, rootSchema, path, ancestors, recursiveReferences, schemaTypes);
                    path.RemoveAt(path.Count - 1);
                    newObject[property.Key] = processedValue;
                }
                else
                {
                    newObject[property.Key] = null;
                }
            }

            if (pushed)
            {
                ancestors.RemoveAt(ancestors.Count - 1);
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
                    path.Add(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    var processedValue = ResolveReferencesRecursive(jsonArray[i]!, rootSchema, path, ancestors, recursiveReferences, schemaTypes);
                    path.RemoveAt(path.Count - 1);
                    newArray.Add(processedValue);
                }
                else
                {
                    newArray.Add(null);
                }
            }
            return newArray;
        }

        // Primitive values are copied as they are.
        return node.DeepClone();
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

        var segments = ParsePointer(refPath);
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
            // Recursion through a polymorphic base type points into its anyOf array.
            else if (current is JsonArray currentArray
                     && int.TryParse(segment, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index)
                     && index < currentArray.Count
                     && currentArray[index] is { } element)
            {
                current = element;
            }
            else
            {
                var partialPath = string.Join('/', segments.Take(i));
                throw new InvalidOperationException($"Failed to resolve reference '{refPath}': cannot navigate beyond '#{partialPath}' - expected object but found {current?.GetType().Name ?? "null"}");
            }
        }

        return current;
    }

    /// <summary>
    /// The unescaped segments of a JSON pointer fragment such as <c>#/properties/children/items</c>; <c>#</c> is the root.
    /// </summary>
    private static string[] ParsePointer(string pointer) =>
        pointer.TrimStart('#').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal))
            .ToArray();

    private static void RemoveSchemaTypeMarkers(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            jsonObject.Remove(SchemaTypeMarker);
            foreach (var property in jsonObject)
            {
                RemoveSchemaTypeMarkers(property.Value);
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var element in jsonArray)
            {
                RemoveSchemaTypeMarkers(element);
            }
        }
    }
}
