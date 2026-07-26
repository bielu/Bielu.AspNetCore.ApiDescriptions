using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>Spec §5.8.8 Failure Action Object. <see cref="Type"/> is "end", "retry", or "goto".</summary>
public sealed class ArazzoFailureAction : IArazzoSerializable, IArazzoExtensible
{
    /// <summary>The unique name of the failure action.</summary>
    public required string Name { get; set; }

    /// <summary>"end", "retry", or "goto".</summary>
    public required string Type { get; set; }

    /// <summary>Only when Type is "goto" or "retry". Mutually exclusive with <see cref="StepId"/>.</summary>
    public string? WorkflowId { get; set; }

    /// <summary>Only when Type is "goto" or "retry". Mutually exclusive with <see cref="WorkflowId"/>.</summary>
    public string? StepId { get; set; }

    /// <summary>Parameters passed to the referenced workflow. The "in" field MUST NOT be used here.</summary>
    public IList<ArazzoReferenceable<ArazzoParameter>>? Parameters { get; set; }

    /// <summary>Only when Type is "retry". Non-negative seconds to delay before retrying.</summary>
    public double? RetryAfter { get; set; }

    /// <summary>Only when Type is "retry". Maximum retry attempts.</summary>
    public int? RetryLimit { get; set; }

    /// <summary>The criteria that determine whether this failure action is applied.</summary>
    public IList<ArazzoCriterion>? Criteria { get; set; }

    /// <summary>Specification extensions keyed by names beginning with <c>x-</c>.</summary>
    public IDictionary<string, System.Text.Json.Nodes.JsonNode?>? Extensions { get; set; }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WritePropertyName("name");
        writer.WriteValue(Name);
        writer.WritePropertyName("type");
        writer.WriteValue(Type);
        writer.WriteOptionalProperty("workflowId", WorkflowId);
        writer.WriteOptionalProperty("stepId", StepId);
        writer.WriteOptionalArrayProperty("parameters", Parameters, p => p.SerializeAsV1(writer));
        writer.WriteOptionalProperty("retryAfter", RetryAfter);
        writer.WriteOptionalProperty("retryLimit", RetryLimit);
        writer.WriteOptionalArrayProperty("criteria", Criteria, c => c.SerializeAsV1(writer));
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}

/// <summary>Failure action type values defined by the Arazzo specification.</summary>
public static class ArazzoFailureActionType
{
    /// <summary>Ends workflow execution.</summary>
    public const string End = "end";

    /// <summary>Retries the failed operation.</summary>
    public const string Retry = "retry";

    /// <summary>Continues execution at another workflow or step.</summary>
    public const string Goto = "goto";
}
