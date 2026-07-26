using System.Text.Json;
using Bielu.Arazzo.Models;

namespace Bielu.Arazzo.Writers;

public static class ArazzoJsonWriter
{
    public static string Write(IArazzoSerializable document, bool indented = true)
    {
        var nodeWriter = new ArazzoJsonNodeWriter();
        document.SerializeAsV1(nodeWriter);
        return nodeWriter.Result?.ToJsonString(new JsonSerializerOptions { WriteIndented = indented }) ?? "null";
    }
}
