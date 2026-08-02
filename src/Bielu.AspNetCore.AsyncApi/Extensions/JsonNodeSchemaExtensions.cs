// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Bielu.AspNetCore.AsyncApi.Schemas;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Constraints;
using AsyncApiConstants = ByteBard.AsyncAPI.Models.AsyncApiConstants;
using AsyncApiJsonSchema = ByteBard.AsyncAPI.Models.AsyncApiJsonSchema;

namespace Bielu.AspNetCore.AsyncApi.Extensions;

/// <summary>
/// Provides a set of extension methods for modifying the opaque JSON Schema type
/// that is provided by the underlying schema generator in System.Text.Json.
/// </summary>
internal static class JsonNodeSchemaExtensions
{
    private static readonly Dictionary<Type, AsyncApiJsonSchema> _simpleTypeToAsyncApiJsonSchema = new()
    {
        [typeof(bool)] = new() { Type = SchemaType.Boolean },
        [typeof(byte)] = new() { Type = SchemaType.Integer, Format = "uint8" },
        [typeof(byte[])] = new() { Type = SchemaType.String, Format = "byte" },
        [typeof(int)] = new() { Type = SchemaType.Integer, Format = "int32" },
        [typeof(uint)] = new() { Type = SchemaType.Integer, Format = "uint32" },
        [typeof(long)] = new() { Type = SchemaType.Integer, Format = "int64" },
        [typeof(ulong)] = new() { Type = SchemaType.Integer, Format = "uint64" },
        [typeof(short)] = new() { Type = SchemaType.Integer, Format = "int16" },
        [typeof(ushort)] = new() { Type = SchemaType.Integer, Format = "uint16" },
        [typeof(float)] = new() { Type = SchemaType.Number, Format = "float" },
        [typeof(double)] = new() { Type = SchemaType.Number, Format = "double" },
        [typeof(decimal)] = new() { Type = SchemaType.Number, Format = "double" },
        [typeof(DateTime)] = new() { Type = SchemaType.String, Format = "date-time" },
        [typeof(DateTimeOffset)] = new() { Type = SchemaType.String, Format = "date-time" },
        [typeof(Guid)] = new() { Type = SchemaType.String, Format = "uuid" },
        [typeof(char)] = new() { Type = SchemaType.String, Format = "char" },
        [typeof(Uri)] = new() { Type = SchemaType.String, Format = "uri" },
        [typeof(string)] = new() { Type = SchemaType.String },
        [typeof(TimeOnly)] = new() { Type = SchemaType.String, Format = "time" },
        [typeof(DateOnly)] = new() { Type = SchemaType.String, Format = "date" },
    };

    /// <summary>
    /// Maps the given validation attributes to the target schema.
    /// </summary>
    /// <remarks>
    /// AsyncApi schema v3 supports the validation vocabulary supported by JSON Schema. Because the underlying
    /// schema generator does not handle validation attributes to the validation vocabulary, we apply that mapping here.
    ///
    /// Note that this method targets <see cref="JsonNode"/> and not <see cref="AsyncApiJsonSchema"/> because it is
    /// designed to be invoked via the `OnGenerated` callback provided by the underlying schema generator
    /// so that attributes can be mapped to the properties associated with inputs and outputs to a given request.
    ///
    /// This implementation only supports mapping validation attributes that have an associated keyword in the
    /// validation vocabulary.
    ///
    /// Validation attributes are applied in a last-wins-order. For example, the following set of attributes:
    ///
    /// [Range(1, 10), Min(5)]
    ///
    /// will result in the schema having a minimum value of 5 and a maximum value of 10. This rule applies even
    /// though the model binding layer in MVC applies all validation attributes on an argument. The following
    /// set of attributes:
    ///
    /// [Base64String]
    /// [Url]
    /// public string Url { get; }
    ///
    /// will result in the schema having a type of "string" and a format of "uri" even though the model binding
    /// layer will validate the string against *both* constraints.
    /// </remarks>
    /// <param name="schema">The <see cref="JsonNode"/> produced by the underlying schema generator.</param>
    /// <param name="validationAttributes">A list of the validation attributes to apply.</param>
    internal static void ApplyValidationAttributes(this JsonNode schema, IEnumerable<Attribute> validationAttributes)
    {
        foreach (var attribute in validationAttributes)
        {
            if (attribute is Base64StringAttribute)
            {
                schema[AsyncApiJsonSchemaKeywords.FormatKeyword] = "byte";
            }
            else if (attribute is RangeAttribute rangeAttribute)
            {
                decimal? minDecimal = null;
                decimal? maxDecimal = null;

                if (rangeAttribute.Minimum is int minimumInteger)
                {
                    // The range was set with the RangeAttribute(int, int) constructor.
                    minDecimal = minimumInteger;
                    maxDecimal = (int)rangeAttribute.Maximum;
                }
                else
                {
                    // Use InvariantCulture if explicitly requested or if the range has been set via the RangeAttribute(double, double) constructor.
                    var targetCulture = rangeAttribute.ParseLimitsInInvariantCulture || rangeAttribute.Minimum is double
                        ? CultureInfo.InvariantCulture
                        : CultureInfo.CurrentCulture;

                    var minString = Convert.ToString(rangeAttribute.Minimum, targetCulture);
                    var maxString = Convert.ToString(rangeAttribute.Maximum, targetCulture);

                    if (decimal.TryParse(minString, NumberStyles.Any, targetCulture, out var value))
                    {
                        minDecimal = value;
                    }
                    if (decimal.TryParse(maxString, NumberStyles.Any, targetCulture, out value))
                    {
                        maxDecimal = value;
                    }
                }

                if (minDecimal is { } minValue)
                {
                    schema[rangeAttribute.MinimumIsExclusive ? AsyncApiJsonSchemaKeywords.ExclusiveMinimum : AsyncApiJsonSchemaKeywords.MinimumKeyword] = minValue;
                }
                if (maxDecimal is { } maxValue)
                {
                    schema[rangeAttribute.MaximumIsExclusive ? AsyncApiJsonSchemaKeywords.ExclusiveMaximum : AsyncApiJsonSchemaKeywords.MaximumKeyword] = maxValue;
                }
            }
            else if (attribute is RegularExpressionAttribute regularExpressionAttribute)
            {
                schema[AsyncApiJsonSchemaKeywords.PatternKeyword] = regularExpressionAttribute.Pattern;
            }
            else if (attribute is MaxLengthAttribute maxLengthAttribute)
            {
                var isArray = MapJsonNodeToSchemaType(schema[AsyncApiJsonSchemaKeywords.TypeKeyword]) is { } schemaTypes && schemaTypes.HasFlag(SchemaType.Array);
                var key = isArray ? AsyncApiJsonSchemaKeywords.MaxItemsKeyword : AsyncApiJsonSchemaKeywords.MaxLengthKeyword;
                schema[key] = maxLengthAttribute.Length;
            }
            else if (attribute is MinLengthAttribute minLengthAttribute)
            {
                var isArray = MapJsonNodeToSchemaType(schema[AsyncApiJsonSchemaKeywords.TypeKeyword]) is { } schemaTypes && schemaTypes.HasFlag(SchemaType.Array);
                var key = isArray ? AsyncApiJsonSchemaKeywords.MinItemsKeyword : AsyncApiJsonSchemaKeywords.MinLengthKeyword;
                schema[key] = minLengthAttribute.Length;
            }
            else if (attribute is LengthAttribute lengthAttribute)
            {
                var isArray = MapJsonNodeToSchemaType(schema[AsyncApiJsonSchemaKeywords.TypeKeyword]) is { } schemaTypes && schemaTypes.HasFlag(SchemaType.Array);
                var targetKeySuffix = isArray ? "Items" : "Length";
                schema[$"min{targetKeySuffix}"] = lengthAttribute.MinimumLength;
                schema[$"max{targetKeySuffix}"] = lengthAttribute.MaximumLength;
            }
            else if (attribute is UrlAttribute)
            {
                schema[AsyncApiJsonSchemaKeywords.FormatKeyword] = "uri";
            }
            else if (attribute is StringLengthAttribute stringLengthAttribute)
            {
                schema[AsyncApiJsonSchemaKeywords.MinLengthKeyword] = stringLengthAttribute.MinimumLength;
                schema[AsyncApiJsonSchemaKeywords.MaxLengthKeyword] = stringLengthAttribute.MaximumLength;
            }
        }
    }

    /// <summary>
    /// Populate the default value into the current schema.
    /// </summary>
    /// <param name="schema">The <see cref="JsonNode"/> produced by the underlying schema generator.</param>
    /// <param name="defaultValue">An object representing the <see cref="object"/> associated with the default value.</param>
    /// <param name="jsonTypeInfo">The <see cref="JsonTypeInfo"/> associated with the target type.</param>
    internal static void ApplyDefaultValue(this JsonNode schema, object? defaultValue, JsonTypeInfo? jsonTypeInfo)
    {
        if (jsonTypeInfo is null)
        {
            return;
        }

        var schemaAttribute = schema.WillBeComponentized()
            ? AsyncApiConstants.DollarRef
            : AsyncApiJsonSchemaKeywords.DefaultKeyword;

        if (defaultValue is null)
        {
            schema[schemaAttribute] = null;
        }
        else
        {
            schema[schemaAttribute] = JsonSerializer.SerializeToNode(defaultValue, jsonTypeInfo);
        }
    }

    /// <summary>
    /// Applies the primitive types and formats to the schema based on the type.
    /// </summary>
    /// <remarks>
    /// AsyncApi v3 requires support for the format keyword in generated types. Because the
    /// underlying schema generator does not support this, we need to manually apply the
    /// supported formats to the schemas associated with the generated type.
    ///
    /// Note that this method targets <see cref="JsonNode"/> and not <see cref="AsyncApiJsonSchema"/> because
    /// it is is designed to be invoked via the `OnGenerated` callback in the underlying schema generator as
    /// opposed to after the generated schemas have been mapped to AsyncApi schemas.
    /// </remarks>
    /// <param name="schema">The <see cref="JsonNode"/> produced by the underlying schema generator.</param>
    /// <param name="context">The <see cref="JsonSchemaExporterContext"/> associated with the <see paramref="schema"/>.</param>
    /// <param name="createSchemaReferenceId">A delegate that generates the reference ID to create for a type.</param>
    internal static void ApplyPrimitiveTypesAndFormats(this JsonNode schema, JsonSchemaExporterContext context, Func<JsonTypeInfo, string?> createSchemaReferenceId)
    {
        var type = context.TypeInfo.Type;
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (_simpleTypeToAsyncApiJsonSchema.TryGetValue(underlyingType ?? type, out var AsyncApiJsonSchema))
        {
            if (underlyingType != null && MapJsonNodeToSchemaType(schema[AsyncApiJsonSchemaKeywords.TypeKeyword]) is { } schemaTypes &&
                !schemaTypes.HasFlag(SchemaType.Null))
            {
                schema[AsyncApiJsonSchemaKeywords.TypeKeyword] = (schemaTypes | SchemaType.Null).ToString();
            }
            schema[AsyncApiJsonSchemaKeywords.FormatKeyword] = AsyncApiJsonSchema.Format;
            schema[AsyncApiConstants.Id] = createSchemaReferenceId(context.TypeInfo);
        }
    }

    // IRouteConstraint is referred to below as plain <c> text rather than a cref: two assemblies in the
    // shared framework declare Microsoft.AspNetCore.Routing.IRouteConstraint, so even the fully
    // qualified name is ambiguous to the doc-comment binder (CS0419) and there is no syntax to pick
    // one. This member is internal, so nothing is lost from the published API reference.
    /// <summary>
    /// Applies route constraints to the target schema.
    /// </summary>
    /// <param name="schema">The <see cref="JsonNode"/> produced by the underlying schema generator.</param>
    /// <param name="constraints">The list of <c>IRouteConstraint</c>s associated with the route parameter.</param>
    internal static void ApplyRouteConstraints(this JsonNode schema, IEnumerable<IRouteConstraint> constraints)
    {
        // Apply constraints in reverse order because when it comes to the routing
        // layer the first constraint that is violated causes routing to short circuit.
        foreach (var constraint in Enumerable.Reverse(constraints))
        {
            if (constraint is MinRouteConstraint minRouteConstraint)
            {
                schema[AsyncApiJsonSchemaKeywords.MinimumKeyword] = minRouteConstraint.Min;
            }
            else if (constraint is MaxRouteConstraint maxRouteConstraint)
            {
                schema[AsyncApiJsonSchemaKeywords.MaximumKeyword] = maxRouteConstraint.Max;
            }
            else if (constraint is MinLengthRouteConstraint minLengthRouteConstraint)
            {
                schema[AsyncApiJsonSchemaKeywords.MinLengthKeyword] = minLengthRouteConstraint.MinLength;
            }
            else if (constraint is MaxLengthRouteConstraint maxLengthRouteConstraint)
            {
                schema[AsyncApiJsonSchemaKeywords.MaxLengthKeyword] = maxLengthRouteConstraint.MaxLength;
            }
            else if (constraint is RangeRouteConstraint rangeRouteConstraint)
            {
                schema[AsyncApiJsonSchemaKeywords.MinimumKeyword] = rangeRouteConstraint.Min;
                schema[AsyncApiJsonSchemaKeywords.MaximumKeyword] = rangeRouteConstraint.Max;
            }
            else if (constraint is RegexRouteConstraint regexRouteConstraint)
            {
                schema[AsyncApiJsonSchemaKeywords.TypeKeyword] = SchemaType.String.ToString();
                schema[AsyncApiJsonSchemaKeywords.FormatKeyword] = null;
                schema[AsyncApiJsonSchemaKeywords.PatternKeyword] = regexRouteConstraint.Constraint.ToString();
            }
            else if (constraint is LengthRouteConstraint lengthRouteConstraint)
            {
                schema[AsyncApiJsonSchemaKeywords.MinLengthKeyword] = lengthRouteConstraint.MinLength;
                schema[AsyncApiJsonSchemaKeywords.MaxLengthKeyword] = lengthRouteConstraint.MaxLength;
            }
            else if (constraint is FloatRouteConstraint or DecimalRouteConstraint or DoubleRouteConstraint)
            {
                schema[AsyncApiJsonSchemaKeywords.TypeKeyword] = SchemaType.Number.ToString();
                schema[AsyncApiJsonSchemaKeywords.FormatKeyword] = constraint is FloatRouteConstraint ? "float" : "double";
            }
            else if (constraint is LongRouteConstraint or IntRouteConstraint)
            {
                schema[AsyncApiJsonSchemaKeywords.TypeKeyword] = SchemaType.Integer.ToString();
                schema[AsyncApiJsonSchemaKeywords.FormatKeyword] = constraint is LongRouteConstraint ? "int64" : "int32";
            }
            else if (constraint is GuidRouteConstraint or StringRouteConstraint)
            {
                schema[AsyncApiJsonSchemaKeywords.TypeKeyword] = SchemaType.String.ToString();
                schema[AsyncApiJsonSchemaKeywords.FormatKeyword] = constraint is GuidRouteConstraint ? "uuid" : null;
            }
            else if (constraint is BoolRouteConstraint)
            {
                schema[AsyncApiJsonSchemaKeywords.TypeKeyword] = SchemaType.Boolean.ToString();
                schema[AsyncApiJsonSchemaKeywords.FormatKeyword] = null;
            }
            else if (constraint is AlphaRouteConstraint)
            {
                schema[AsyncApiJsonSchemaKeywords.TypeKeyword] = SchemaType.String.ToString();
                schema[AsyncApiJsonSchemaKeywords.FormatKeyword] = null;
            }
            else if (constraint is DateTimeRouteConstraint)
            {
                schema[AsyncApiJsonSchemaKeywords.TypeKeyword] = SchemaType.String.ToString();
                schema[AsyncApiJsonSchemaKeywords.FormatKeyword] = "date-time";
            }
        }
    }

    /// <summary>
    /// Applies parameter-specific customizations to the target schema.
    /// </summary>
    /// <param name="schema">The <see cref="JsonNode"/> produced by the underlying schema generator.</param>
    /// <param name="parameterDescription">The <see cref="ApiParameterDescription"/> associated with the <see paramref="schema"/>.</param>
    /// <param name="jsonTypeInfo">The <see cref="JsonTypeInfo"/> associated with the <see paramref="schema"/>.</param>
    internal static void ApplyParameterInfo(this JsonNode schema, ApiParameterDescription parameterDescription, JsonTypeInfo? jsonTypeInfo)
    {
        // This is special handling for parameters that are not bound from the body but represented in a complex type.
        // For example:
        //
        // public class MyArgs
        // {
        //     [Required]
        //     [Range(1, 10)]
        //     [FromQuery]
        //     public string Name { get; set; }
        // }
        //
        // public IActionResult(MyArgs myArgs) { }
        //
        // In this case, the `ApiParameterDescription` object that we received will represent the `Name` property
        // based on our model binding heuristics. In that case, to access the validation attributes that the
        // model binder will respect we will need to get the property from the container type and map the
        // attributes on it to the schema.
        if (parameterDescription.ModelMetadata is { PropertyName: { }, ContainerType: { }, HasValidators: true, ValidatorMetadata: { } validations })
        {
            var attributes = validations.OfType<ValidationAttribute>();
            schema.ApplyValidationAttributes(attributes);
        }
        if (parameterDescription.ModelMetadata is DefaultModelMetadata { Attributes.PropertyAttributes.Count: > 0 } metadata &&
            metadata.Attributes.PropertyAttributes.OfType<DefaultValueAttribute>().LastOrDefault() is { } metadataDefaultValueAttribute)
        {
            schema.ApplyDefaultValue(metadataDefaultValueAttribute.Value, jsonTypeInfo);
        }
        if (parameterDescription.ParameterDescriptor is IParameterInfoParameterDescriptor { ParameterInfo: { } parameterInfo })
        {
            if (parameterInfo.HasDefaultValue)
            {
                schema.ApplyDefaultValue(parameterInfo.DefaultValue, jsonTypeInfo);
            }
            else if (parameterInfo.GetCustomAttributes<DefaultValueAttribute>().LastOrDefault() is { } defaultValueAttribute)
            {
                schema.ApplyDefaultValue(defaultValueAttribute.Value, jsonTypeInfo);
            }

            if (parameterInfo.GetCustomAttributes<ValidationAttribute>() is { } validationAttributes)
            {
                schema.ApplyValidationAttributes(validationAttributes);
            }
        }
        // Route constraints are only defined on parameters that are sourced from the path. Since
        // they are encoded in the route template, and not in the type information based to the underlying
        // schema generator we have to handle them separately here.
        if (parameterDescription.RouteInfo?.Constraints is { } constraints)
        {
            schema.ApplyRouteConstraints(constraints);
        }

        if (parameterDescription.Source is { } bindingSource
            && SupportsNullableProperty(bindingSource)
            && MapJsonNodeToSchemaType(schema[AsyncApiJsonSchemaKeywords.TypeKeyword]) is { } schemaTypes &&
            schemaTypes.HasFlag(SchemaType.Null))
        {
            schema[AsyncApiJsonSchemaKeywords.TypeKeyword] = (schemaTypes & ~SchemaType.Null).ToString();
        }

        // Parameters sourced from the header, query, route, and/or form cannot be nullable based on our binding
        // rules but can be optional.
        static bool SupportsNullableProperty(BindingSource bindingSource) => bindingSource == BindingSource.Header
            || bindingSource == BindingSource.Query
            || bindingSource == BindingSource.Path
            || bindingSource == BindingSource.Form
            || bindingSource == BindingSource.FormFile;
    }

    /// <summary>
    /// Applies the polymorphism options defined by System.Text.Json to the target schema following AsyncApi v3's
    /// conventions for the discriminator property.
    /// </summary>
    /// <param name="schema">The <see cref="JsonNode"/> produced by the underlying schema generator.</param>
    /// <param name="context">The <see cref="JsonSchemaExporterContext"/> associated with the current type.</param>
    /// <param name="createSchemaReferenceId">A delegate that generates the reference ID to create for a type.</param>
    internal static void MapPolymorphismOptionsToDiscriminator(this JsonNode schema, JsonSchemaExporterContext context, Func<JsonTypeInfo, string?> createSchemaReferenceId)
    {
        // The `context.BaseTypeInfo == null` check is used to ensure that we only apply the polymorphism options
        // to the top-level schema and not to any nested schemas that are generated.
        if (context.TypeInfo.PolymorphismOptions is { } polymorphismOptions && context.BaseTypeInfo == null)
        {
            // System.Text.Json supports serializing to a non-abstract base class if no discriminator is provided.
            // AsyncApi requires that all polymorphic sub-schemas have an associated discriminator. If the base type
            // doesn't declare itself as its own derived type via [JsonDerived], then it can't have a discriminator,
            // which AsyncApi requires. In that case, we exit early to avoid mapping the polymorphism options
            // to the `discriminator` property and return an un-discriminated `anyOf` schema instead.
            if (IsNonAbstractTypeWithoutDerivedTypeReference(context))
            {
                return;
            }
            var mappings = new JsonObject();
            foreach (var derivedType in polymorphismOptions.DerivedTypes)
            {
                if (derivedType.TypeDiscriminator is { } discriminator)
                {
                    var jsonDerivedType = context.TypeInfo.Options.GetTypeInfo(derivedType.DerivedType);
                    // Discriminator mappings are only supported in AsyncApi v3+ so we can safely assume that
                    // the generated reference mappings will support the AsyncApi v3 schema reference format
                    // that we hardcode here. We could use `AsyncApiReference` to construct the reference and
                    // serialize it but we use a hardcoded string here to avoid allocating a new object and
                    // working around Microsoft.AsyncApi's serialization libraries.
                    mappings[$"{discriminator}"] = $"{createSchemaReferenceId(context.TypeInfo)}{createSchemaReferenceId(jsonDerivedType)}";
                }
            }
            schema[AsyncApiJsonSchemaKeywords.DiscriminatorKeyword] = polymorphismOptions.TypeDiscriminatorPropertyName;
            schema[AsyncApiJsonSchemaKeywords.DiscriminatorMappingKeyword] = mappings;
        }
    }

    /// <summary>
    /// Set the x-schema-id property on the schema to the identifier associated with the type.
    /// </summary>
    /// <param name="schema">The <see cref="JsonNode"/> produced by the underlying schema generator.</param>
    /// <param name="context">The <see cref="JsonSchemaExporterContext"/> associated with the current type.</param>
    /// <param name="createSchemaReferenceId">A delegate that generates the reference ID to create for a type.</param>
    internal static void ApplySchemaReferenceId(this JsonNode schema, JsonSchemaExporterContext context, Func<JsonTypeInfo, string?> createSchemaReferenceId)
    {
        if (createSchemaReferenceId(context.TypeInfo) is { } schemaReferenceId)
        {
            schema[AsyncApiConstants.Id] = schemaReferenceId;
        }
        // If the type is a non-abstract base class that is not one of the derived types then mark it as a base schema.
        if (context.BaseTypeInfo == context.TypeInfo &&
            IsNonAbstractTypeWithoutDerivedTypeReference(context))
        {
            schema[AsyncApiConstants.Id] = "Base";
        }
    }

    /// <summary>
    /// Determines whether the specified JSON schema will be moved into the components section.
    /// </summary>
    /// <param name="schema">The <see cref="JsonNode"/> produced by the underlying schema generator.</param>
    /// <returns><see langword="true"/> if the schema will be componentized; otherwise, <see langword="false"/>.</returns>
    internal static bool WillBeComponentized(this JsonNode schema)
        => schema.WillBeComponentized(out _);

    /// <summary>
    /// Determines whether the specified JSON schema node contains a componentized schema identifier.
    /// </summary>
    /// <param name="schema">The JSON schema node to inspect for a componentized schema identifier.</param>
    /// <param name="schemaId">When this method returns <see langword="true"/>, contains the schema identifier found in the node; otherwise,
    /// <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the schema will be componentized; otherwise, <see langword="false"/>.</returns>
    internal static bool WillBeComponentized(this JsonNode schema, [NotNullWhen(true)] out string? schemaId)
    {
        if (schema[AsyncApiConstants.Id] is JsonNode schemaIdNode
            && schemaIdNode.GetValueKind() == JsonValueKind.String)
        {
            schemaId = schemaIdNode.GetValue<string>();
            if (!string.IsNullOrEmpty(schemaId))
            {
                return true;
            }
        }
        schemaId = null;
        return false;
    }

    /// <summary>
    /// Returns <langword ref="true" /> if the current type is a non-abstract base class that is not defined as its
    /// own derived type.
    /// </summary>
    /// <param name="context">The <see cref="JsonSchemaExporterContext"/> associated with the current type.</param>
    private static bool IsNonAbstractTypeWithoutDerivedTypeReference(JsonSchemaExporterContext context)
    {
        return !context.TypeInfo.Type.IsAbstract
            && context.TypeInfo.PolymorphismOptions is { } polymorphismOptions
            && !polymorphismOptions.DerivedTypes.Any(type => type.DerivedType == context.TypeInfo.Type);
    }

    /// <summary>
    /// Support applying nullability status for reference types provided as a property or field.
    /// </summary>
    /// <param name="schema">The <see cref="JsonNode"/> produced by the underlying schema generator.</param>
    /// <param name="propertyInfo">The <see cref="JsonPropertyInfo" /> associated with the schema.</param>
    internal static void ApplyNullabilityContextInfo(this JsonNode schema, JsonPropertyInfo propertyInfo)
    {
        // Avoid setting explicit nullability annotations for `object` types so they continue to match on the catch
        // all schema (no type, no format, no constraints).
        if (propertyInfo.PropertyType != typeof(object) && (propertyInfo.IsGetNullable || propertyInfo.IsSetNullable))
        {
            if (MapJsonNodeToSchemaType(schema[AsyncApiJsonSchemaKeywords.TypeKeyword]) is { } schemaTypes &&
                !schemaTypes.HasFlag(SchemaType.Null))
            {
                schema[AsyncApiJsonSchemaKeywords.TypeKeyword] = (schemaTypes | SchemaType.Null).ToString();
            }
        }
        if (schema.WillBeComponentized() &&
            propertyInfo.PropertyType != typeof(object) && propertyInfo.ShouldApplyNullablePropertySchema())
        {
            schema[AsyncApiGeneratorConstants.NullableProperty] = true;
        }
    }

    /// <summary>
    /// Prunes the "null" type from the schema for types that are componentized. These
    /// types should represent their nullability using oneOf with null instead.
    /// </summary>
    /// <param name="schema">The <see cref="JsonNode"/> produced by the underlying schema generator.</param>
    internal static void PruneNullTypeForComponentizedTypes(this JsonNode schema)
    {
        if (schema.WillBeComponentized() &&
                schema[AsyncApiJsonSchemaKeywords.TypeKeyword] is JsonArray typeArray)
        {
            for (var i = typeArray.Count - 1; i >= 0; i--)
            {
                if (typeArray[i]?.GetValue<string>() == "null")
                {
                    typeArray.RemoveAt(i);
                }
            }
            if (typeArray.Count == 1)
            {
                schema[AsyncApiJsonSchemaKeywords.TypeKeyword] = typeArray[0]?.GetValue<string>();
            }
        }
    }

    private static SchemaType? MapJsonNodeToSchemaType(JsonNode? jsonNode)
    {
        if (jsonNode is not JsonArray jsonArray)
        {
            if (Enum.TryParse<SchemaType>(jsonNode?.GetValue<string>(), true, out var AsyncApiSchemaType))
            {
                return AsyncApiSchemaType;
            }

            return jsonNode is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var identifier)
                ? ToSchemaType(identifier)
                : null;
        }

        SchemaType? schemaType = null;

        foreach (var node in jsonArray)
        {
            if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var identifier))
            {
                var type = ToSchemaType(identifier);
                schemaType = schemaType.HasValue ? (schemaType | type) : type;
            }
        }

        return schemaType;

        static SchemaType ToSchemaType(string identifier)
        {
            return identifier.ToLowerInvariant() switch
            {
                "null" => SchemaType.Null,
                "boolean" => SchemaType.Boolean,
                "integer" => SchemaType.Integer,
                "number" => SchemaType.Number,
                "string" => SchemaType.String,
                "array" => SchemaType.Array,
                "object" => SchemaType.Object,
                _ => throw new InvalidOperationException($"Unknown schema type: {identifier}"),
            };
        }
    }
}
