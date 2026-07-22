// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Schemas;

[JsonSerializable(typeof(AsyncApiJsonSchema))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonArray))]
[JsonSerializable(typeof(AsyncApiProblemDetails))]
internal sealed partial class AsyncApiJsonSchemaContext : JsonSerializerContext { }

internal record AsyncApiProblemDetails(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("detail")] string Detail);
