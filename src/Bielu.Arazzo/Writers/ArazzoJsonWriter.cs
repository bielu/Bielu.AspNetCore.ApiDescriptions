using System.Text.Json;
using Bielu.Arazzo.Models;

namespace Bielu.Arazzo.Writers;

/// <summary>Writes Arazzo models as JSON text.</summary>
public static class ArazzoJsonWriter
{
    /// <summary>Serializes <paramref name="document"/> to an Arazzo JSON document.</summary>
    /// <param name="document">The model to serialize.</param>
    /// <param name="indented">Whether the resulting JSON should include indentation.</param>
    /// <returns>The serialized JSON document.</returns>
    public static string Write(IArazzoSerializable document, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(document);

        var nodeWriter = new ArazzoJsonNodeWriter();
        document.SerializeAsV1(nodeWriter);
        return nodeWriter.Result?.ToJsonString(new JsonSerializerOptions { WriteIndented = indented }) ?? "null";
    }
}
