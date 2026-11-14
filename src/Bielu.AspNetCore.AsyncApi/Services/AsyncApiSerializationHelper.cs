// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Services;

/// <summary>
/// Helper class for AsyncAPI document serialization.
/// Provides methods to serialize AsyncAPI documents with proper schema compliance.
/// </summary>
internal static class AsyncApiSerializationHelper
{
    /// <summary>
    /// Serializes an AsyncAPI V2 document to JSON.
    /// </summary>
    /// <remarks>
    /// The output is ByteBard's, as for 3.x. It used to be parsed and written again with
    /// <see cref="System.Text.Json"/>'s default options, which minified it and escaped every non-ASCII character
    /// (and <c>+</c>, <c>&lt;</c>, <c>&gt;</c>) for no gain: the step meant to add a missing <c>channels</c> object
    /// had been commented out. A document without channels is rejected before serialization instead.
    /// </remarks>
    /// <param name="document">The AsyncAPI document to serialize.</param>
    /// <returns>JSON string representation of the document.</returns>
    public static string SerializeV2ToJson(AsyncApiDocument document)
    {
        using var stringWriter = new StringWriter();
        var jsonWriter = new AsyncApiJsonWriter(stringWriter);
        document.SerializeV2(jsonWriter);
        return stringWriter.ToString();
    }

    /// <summary>
    /// Serializes an AsyncAPI V2 document to YAML.
    /// </summary>
    /// <param name="document">The AsyncAPI document to serialize.</param>
    /// <returns>YAML string representation of the document.</returns>
    public static string SerializeV2ToYaml(AsyncApiDocument document)
    {
        using var stringWriter = new StringWriter();
        var yamlWriter = new AsyncApiYamlWriter(stringWriter, null);
        document.SerializeV2(yamlWriter);
        return stringWriter.ToString();
    }

    /// <summary>
    /// Serializes an AsyncAPI V3 document to JSON.
    /// Uses StringWriter to avoid streaming issues with IIS in-process mode
    /// where writing directly to the response PipeWriter can cause truncated output.
    /// </summary>
    /// <param name="document">The AsyncAPI document to serialize.</param>
    /// <returns>JSON string representation of the document.</returns>
    public static string SerializeV3ToJson(AsyncApiDocument document)
    {
        using var stringWriter = new StringWriter();
        var jsonWriter = new AsyncApiJsonWriter(stringWriter);
        document.SerializeV3(jsonWriter);
        return stringWriter.ToString();
    }

    /// <summary>
    /// Serializes an AsyncAPI V3 document to YAML.
    /// Uses StringWriter to avoid streaming issues with IIS in-process mode
    /// where writing directly to the response PipeWriter can cause truncated output.
    /// </summary>
    /// <param name="document">The AsyncAPI document to serialize.</param>
    /// <returns>YAML string representation of the document.</returns>
    public static string SerializeV3ToYaml(AsyncApiDocument document)
    {
        using var stringWriter = new StringWriter();
        var yamlWriter = new AsyncApiYamlWriter(stringWriter, null);
        document.SerializeV3(yamlWriter);
        return stringWriter.ToString();
    }
}
