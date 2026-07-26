using System.Text.Json.Nodes;
using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>
/// Spec §5.8.9 Components Object. Scoped to the document it is defined in — a step in document A that
/// references a workflow in document B does not see A's components when evaluating B's workflow.
/// </summary>
public sealed class ArazzoComponents : IArazzoSerializable, IArazzoExtensible
{
    /// <summary>Reusable JSON Schema objects referenced from workflow inputs via standard <c>$ref</c>.</summary>
    public IDictionary<string, JsonNode?>? Inputs { get; set; }

    public IDictionary<string, ArazzoParameter>? Parameters { get; set; }

    public IDictionary<string, ArazzoSuccessAction>? SuccessActions { get; set; }

    public IDictionary<string, ArazzoFailureAction>? FailureActions { get; set; }

    public IDictionary<string, JsonNode?>? Extensions { get; set; }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        writer.WriteOptionalMapProperty("inputs", Inputs, writer.WriteRaw);
        writer.WriteOptionalMapProperty("parameters", Parameters, p => p.SerializeAsV1(writer));
        writer.WriteOptionalMapProperty("successActions", SuccessActions, a => a.SerializeAsV1(writer));
        writer.WriteOptionalMapProperty("failureActions", FailureActions, a => a.SerializeAsV1(writer));
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
