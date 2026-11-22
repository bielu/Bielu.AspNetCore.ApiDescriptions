using System.Text.Json.Nodes;

namespace Bielu.Arazzo.Validation;

/// <summary>How a step's target reference fared against an <see cref="ArazzoWorkspace"/>.</summary>
public enum ArazzoStepTargetStatus
{
    /// <summary>The reference points at something in a registered source document.</summary>
    Resolved,

    /// <summary>The reference doesn't resolve, or names a source the document doesn't declare.</summary>
    Unresolved,

    /// <summary>The workspace has no document for the source, so the reference wasn't checked.</summary>
    Skipped,
}

/// <summary>
/// One workflow step's <c>operationId</c>, <c>operationPath</c> or <c>channelPath</c>, and what it resolved to.
/// Returned by <see cref="ArazzoReferenceValidator.ResolveStepTargets"/>.
/// </summary>
/// <param name="WorkflowId">The step's workflow.</param>
/// <param name="StepId">The step.</param>
/// <param name="Path">The JSON Pointer of the step's reference field, such as <c>/workflows/0/steps/1/channelPath</c>.</param>
/// <param name="Reference">The reference as written in the document.</param>
/// <param name="SourceName">The source the reference resolved in or names, or <see langword="null"/> for an unqualified <c>operationId</c> that resolved nowhere.</param>
/// <param name="Status">Whether the reference resolved, didn't, or wasn't checked.</param>
/// <param name="Target">The operation or channel it resolved to, when <paramref name="Status"/> is <see cref="ArazzoStepTargetStatus.Resolved"/>.</param>
/// <param name="Message">Why it didn't resolve, when <paramref name="Status"/> is <see cref="ArazzoStepTargetStatus.Unresolved"/>.</param>
public sealed record ArazzoStepTarget(
    string WorkflowId,
    string StepId,
    string Path,
    string Reference,
    string? SourceName,
    ArazzoStepTargetStatus Status,
    JsonNode? Target,
    string? Message);
