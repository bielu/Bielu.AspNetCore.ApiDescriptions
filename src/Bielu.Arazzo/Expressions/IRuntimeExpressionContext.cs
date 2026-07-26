using System.Text.Json.Nodes;

namespace Bielu.Arazzo.Expressions;

/// <summary>
/// Supplies the data a <see cref="RuntimeExpressionEvaluator"/> needs to resolve an expression. HTTP- and
/// message-specific sourcing ($url/$method/$statusCode/$request/$response/$message) is left to the
/// caller's implementation — an ASP.NET-hosted HTTP client for OpenAPI steps, a broker/SignalR/gRPC
/// client for AsyncAPI steps (see ARAZZO-PROPOSAL.md §3.D). Everything else ($inputs/$outputs/$steps/
/// $workflows/$sourceDescriptions/$components/$self) is pure data lookup and does not depend on any
/// transport.
/// </summary>
public interface IRuntimeExpressionContext
{
    string? Self { get; }

    string? Url { get; }

    string? Method { get; }

    int? StatusCode { get; }

    JsonNode? GetRequestValue(RuntimeExpressionSource source);

    JsonNode? GetResponseValue(RuntimeExpressionSource source);

    JsonNode? GetMessageValue(RuntimeExpressionSource source);

    JsonNode? GetInput(string name);

    /// <summary>The current evaluation scope's own outputs map (e.g. a workflow referencing its own already-computed outputs).</summary>
    JsonNode? GetOutput(string name);

    JsonNode? GetStepOutput(string stepId, string outputName);

    JsonNode? GetWorkflowField(string workflowName, string field, string? fieldName);

    JsonNode? GetSourceDescriptionReference(string sourceName, string referenceId);

    JsonNode? GetComponent(string field, string name);
}
