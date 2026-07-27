using System.Text.Json.Nodes;
using Bielu.Arazzo;
using Bielu.Arazzo.Models;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.Arazzo.SourceResolvers;

/// <summary>
/// Resolves Arazzo <c>operationId</c>/<c>channelPath</c> references against a live <see cref="AsyncApiDocument"/>.
/// Registered for <see cref="ArazzoSourceDescriptionType.AsyncApi"/> sources by <c>AddArazzo</c>'s self-wiring.
/// </summary>
public sealed class AsyncApiSourceResolver : IArazzoSourceResolver
{
    /// <inheritdoc />
    public string SourceType => ArazzoSourceDescriptionType.AsyncApi;

    /// <inheritdoc />
    public bool TryResolveOperation(object document, string operationId, out JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(operationId);

        operation = null;
        if (document is not AsyncApiDocument asyncApiDocument ||
            !asyncApiDocument.Operations.TryGetValue(operationId, out var candidate))
        {
            return false;
        }

        operation = Serialize(candidate);
        return true;
    }

    /// <summary>AsyncAPI operations are not addressed by JSON Pointer in this package's builder; resolves an <c>/operations/{id}</c> pointer as a convenience, otherwise returns false.</summary>
    public bool TryResolveOperationPath(object document, string jsonPointer, out JsonNode? operation)
    {
        operation = null;
        if (document is not AsyncApiDocument asyncApiDocument || !TryParseMapPointer(jsonPointer, "/operations/", out var name))
        {
            return false;
        }

        if (!asyncApiDocument.Operations.TryGetValue(name, out var candidate))
        {
            return false;
        }

        operation = Serialize(candidate);
        return true;
    }

    /// <summary>Resolves an <c>/channels/{name}</c> JSON Pointer, as produced by <c>ArazzoStepBuilder.Channel</c>.</summary>
    public bool TryResolveChannelPath(object document, string jsonPointer, out JsonNode? channel)
    {
        channel = null;
        if (document is not AsyncApiDocument asyncApiDocument || !TryParseMapPointer(jsonPointer, "/channels/", out var name))
        {
            return false;
        }

        if (!asyncApiDocument.Channels.TryGetValue(name, out var candidate))
        {
            return false;
        }

        channel = Serialize(candidate);
        return true;
    }

    private static bool TryParseMapPointer(string jsonPointer, string prefix, out string name)
    {
        name = string.Empty;
        if (!jsonPointer.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        name = jsonPointer[prefix.Length..].Replace("~1", "/").Replace("~0", "~");
        return true;
    }

    private static JsonNode? Serialize(AsyncApiOperation operation) => Serialize(operation.SerializeV3);

    private static JsonNode? Serialize(AsyncApiChannel channel) => Serialize(channel.SerializeV3);

    private static JsonNode? Serialize(Action<IAsyncApiWriter> serialize)
    {
        using var stringWriter = new StringWriter();
        var writer = new AsyncApiJsonWriter(stringWriter);
        serialize(writer);
        return JsonNode.Parse(stringWriter.ToString());
    }
}
