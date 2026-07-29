using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>
/// Spec §5.8.5 Step Object. <see cref="ChannelPath"/>, <see cref="Action"/>, and <see cref="CorrelationId"/>
/// are the AsyncAPI-specific fields new in 1.1.0: a step targets either an operation
/// (<see cref="OperationId"/>/<see cref="OperationPath"/>), a workflow (<see cref="WorkflowId"/>), or —
/// for AsyncAPI sources — a channel (<see cref="ChannelPath"/>) with an explicit send/receive
/// <see cref="Action"/>. The specification defines these AsyncAPI step semantics precisely; they are not
/// left to interpretation.
/// </summary>
public sealed class ArazzoStep : IArazzoSerializable, IArazzoExtensible
{
    public string? Description { get; set; }

    public required string StepId { get; set; }

    /// <summary>Mutually exclusive with <see cref="OperationPath"/>, <see cref="ChannelPath"/>, and <see cref="WorkflowId"/>.</summary>
    public string? OperationId { get; set; }

    /// <summary>A Source Description + JSON Pointer to an OpenAPI operation. Mutually exclusive with the other three targets.</summary>
    public string? OperationPath { get; set; }

    /// <summary>A Source Description + JSON Pointer to an AsyncAPI channel. Mutually exclusive with the other three targets.</summary>
    public string? ChannelPath { get; set; }

    /// <summary>Mutually exclusive with the other three targets.</summary>
    public string? WorkflowId { get; set; }

    public IList<ArazzoReferenceable<ArazzoParameter>>? Parameters { get; set; }

    public ArazzoRequestBody? RequestBody { get; set; }

    public IList<ArazzoCriterion>? SuccessCriteria { get; set; }

    public IList<ArazzoReferenceable<ArazzoSuccessAction>>? OnSuccess { get; set; }

    public IList<ArazzoReferenceable<ArazzoFailureAction>>? OnFailure { get; set; }

    public IDictionary<string, ArazzoValue>? Outputs { get; set; }

    /// <summary>Maximum milliseconds to wait for the step before aborting and failing it.</summary>
    public int? Timeout { get; set; }

    /// <summary>
    /// AsyncAPI-only: links a request to its response, matching a correlationId defined in the AsyncAPI
    /// document. Only applicable when <see cref="Action"/> is "receive".
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>AsyncAPI-only: "send" (publish) or "receive" (subscribe).</summary>
    public string? Action { get; set; }

    /// <summary>
    /// stepIds that must complete before this step executes. Primarily for async coordination — see
    /// spec §5.8.5.2: unnecessary for purely synchronous workflows, where steps ordering in the array
    /// alone is the recommended approach.
    /// </summary>
    public IList<string>? DependsOn { get; set; }

    public IDictionary<string, System.Text.Json.Nodes.JsonNode?>? Extensions { get; set; }

    public void SerializeAsV1(IArazzoWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteOptionalProperty("description", Description);
        writer.WritePropertyName("stepId");
        writer.WriteValue(StepId);
        writer.WriteOptionalProperty("operationId", OperationId);
        writer.WriteOptionalProperty("operationPath", OperationPath);
        writer.WriteOptionalProperty("channelPath", ChannelPath);
        writer.WriteOptionalProperty("workflowId", WorkflowId);
        writer.WriteOptionalArrayProperty("parameters", Parameters, p => p.SerializeAsV1(writer));
        if (RequestBody is not null)
        {
            writer.WritePropertyName("requestBody");
            RequestBody.SerializeAsV1(writer);
        }

        writer.WriteOptionalArrayProperty("successCriteria", SuccessCriteria, c => c.SerializeAsV1(writer));
        writer.WriteOptionalArrayProperty("onSuccess", OnSuccess, a => a.SerializeAsV1(writer));
        writer.WriteOptionalArrayProperty("onFailure", OnFailure, a => a.SerializeAsV1(writer));
        writer.WriteOptionalMapProperty("outputs", Outputs, v => v.SerializeAsV1(writer));
        writer.WriteOptionalProperty("timeout", Timeout);
        writer.WriteOptionalProperty("correlationId", CorrelationId);
        writer.WriteOptionalProperty("action", Action);
        writer.WriteOptionalArrayProperty("dependsOn", DependsOn, writer.WriteValue);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}

public static class ArazzoStepAction
{
    public const string Send = "send";
    public const string Receive = "receive";
}
