using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>Spec §5.8.7 Success Action Object. <see cref="Type"/> is "end" or "goto".</summary>
public sealed class ArazzoSuccessAction : IArazzoSerializable, IArazzoExtensible
{
    public required string Name { get; set; }

    /// <summary>"end" or "goto".</summary>
    public required string Type { get; set; }

    /// <summary>Only when Type is "goto". Mutually exclusive with <see cref="StepId"/>.</summary>
    public string? WorkflowId { get; set; }

    /// <summary>Only when Type is "goto". Mutually exclusive with <see cref="WorkflowId"/>.</summary>
    public string? StepId { get; set; }

    public IList<ArazzoCriterion>? Criteria { get; set; }

    public IDictionary<string, System.Text.Json.Nodes.JsonNode?>? Extensions { get; set; }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("name");
        writer.WriteValue(Name);
        writer.WritePropertyName("type");
        writer.WriteValue(Type);
        writer.WriteOptionalProperty("workflowId", WorkflowId);
        writer.WriteOptionalProperty("stepId", StepId);
        writer.WriteOptionalArrayProperty("criteria", Criteria, c => c.SerializeAsV1(writer));
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}

public static class ArazzoSuccessActionType
{
    public const string End = "end";
    public const string Goto = "goto";
}
