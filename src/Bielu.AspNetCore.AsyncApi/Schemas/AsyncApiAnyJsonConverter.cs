// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Schemas;

/// <summary>
/// Converts <see cref="AsyncApiAny"/> to/from arbitrary JSON.
/// </summary>
/// <remarks>
/// <see cref="AsyncApiAny"/> wraps an opaque <see cref="JsonNode"/> and has no parameterless
/// constructor, so System.Text.Json's default reflection-based (or source-generated) object
/// converter cannot construct one on its own — any exported JSON schema containing an
/// <c>AsyncApiJsonSchema</c> property typed <c>AsyncApiAny</c> or a collection of it (<c>Enum</c>,
/// <c>Default</c>, <c>Const</c>, <c>Example</c>, ...) previously threw
/// <see cref="JsonException"/> as soon as that property carried a value — most commonly triggered by
/// any enum property serialized with <c>JsonStringEnumConverter</c>, since
/// <see cref="System.Text.Json.Schema.JsonSchemaExporter"/> emits an <c>"enum"</c> keyword for those,
/// and <c>Bielu.AspNetCore.AsyncApi.Services.Schemas.AsyncApiJsonSchemaService</c> deserializes the exported schema straight into
/// <c>ByteBard.AsyncAPI.Models.AsyncApiJsonSchema</c>.
/// </remarks>
internal sealed class AsyncApiAnyJsonConverter : JsonConverter<AsyncApiAny>
{
    /// <inheritdoc />
    public override AsyncApiAny? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var node = JsonNode.Parse(ref reader);
        return node is null ? null : new AsyncApiAny(node);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, AsyncApiAny value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        var node = value.GetNode();
        if (node is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            node.WriteTo(writer);
        }
    }
}
