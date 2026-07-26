using System.Text.Json.Nodes;
using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>Spec §5.8.4 Workflow Object.</summary>
public sealed class ArazzoWorkflow : IArazzoSerializable, IArazzoExtensible
{
    public required string WorkflowId { get; set; }

    public string? Summary { get; set; }

    public string? Description { get; set; }

    /// <summary>A JSON Schema 2020-12 object describing this workflow's input parameters.</summary>
    public JsonNode? Inputs { get; set; }

    /// <summary>workflowIds that must complete before this workflow can be processed.</summary>
    public IList<string>? DependsOn { get; set; }

    public required IList<ArazzoStep> Steps { get; set; }

    /// <summary>Success actions applicable to every step in this workflow; steps may override, never remove.</summary>
    public IList<ArazzoReferenceable<ArazzoSuccessAction>>? SuccessActions { get; set; }

    /// <summary>Failure actions applicable to every step in this workflow; steps may override, never remove.</summary>
    public IList<ArazzoReferenceable<ArazzoFailureAction>>? FailureActions { get; set; }

    public IDictionary<string, ArazzoValue>? Outputs { get; set; }

    /// <summary>Parameters applicable to every step in this workflow; steps may override, never remove.</summary>
    public IList<ArazzoReferenceable<ArazzoParameter>>? Parameters { get; set; }

    public IDictionary<string, JsonNode?>? Extensions { get; set; }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("workflowId");
        writer.WriteValue(WorkflowId);
        writer.WriteOptionalProperty("summary", Summary);
        writer.WriteOptionalProperty("description", Description);
        writer.WriteOptionalProperty("inputs", Inputs);
        writer.WriteOptionalArrayProperty("dependsOn", DependsOn, writer.WriteValue);
        writer.WritePropertyName("steps");
        writer.WriteStartArray();
        foreach (var step in Steps)
        {
            step.SerializeAsV1(writer);
        }

        writer.WriteEndArray();
        writer.WriteOptionalArrayProperty("successActions", SuccessActions, a => a.SerializeAsV1(writer));
        writer.WriteOptionalArrayProperty("failureActions", FailureActions, a => a.SerializeAsV1(writer));
        writer.WriteOptionalMapProperty("outputs", Outputs, v => v.SerializeAsV1(writer));
        writer.WriteOptionalArrayProperty("parameters", Parameters, p => p.SerializeAsV1(writer));
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
