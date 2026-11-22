using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bielu.Arazzo.Models;

namespace Bielu.Arazzo.Validation;

/// <summary>
/// Checks that every workflow step's <c>operationId</c>, <c>operationPath</c> and <c>channelPath</c> points at
/// something that exists in the source documents registered in an <see cref="ArazzoWorkspace"/>. Where
/// <see cref="ArazzoValidator"/> checks a document on its own, this checks it against the APIs it describes, so a
/// renamed operation or removed channel is caught before the workflow ships.
/// </summary>
/// <remarks>
/// <para>
/// Only references into a source with a document registered in the workspace are resolved. A source the document
/// declares but the workspace has no document for, such as another service's API reached by URL, is skipped;
/// <see cref="UncheckedSources"/> lists those, so a caller can report what wasn't checked. A reference that names a
/// source the document doesn't declare at all is always reported.
/// </para>
/// <para>
/// An unqualified <c>operationId</c> resolves when any registered source has it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var workspace = new ArazzoWorkspace();
/// workspace.RegisterResolver(new AsyncApiJsonSourceResolver());
/// workspace.RegisterDocument("events", "asyncapi", asyncApiJson);
///
/// foreach (var error in ArazzoReferenceValidator.Validate(document, workspace))
/// {
///     Console.WriteLine(error);
/// }
/// </code>
/// </example>
public static partial class ArazzoReferenceValidator
{
    [GeneratedRegex(@"^\{\$sourceDescriptions\.([A-Za-z0-9_-]+)\.url\}#(.*)$")]
    private static partial Regex SourceReferencePattern();

    [GeneratedRegex(@"^\$sourceDescriptions\.([A-Za-z0-9_-]+)\.(.+)$")]
    private static partial Regex QualifiedOperationIdPattern();

    /// <summary>
    /// Resolves every step reference in <paramref name="document"/> against <paramref name="workspace"/>. Each
    /// finding's message starts with <c>{workflowId}.{stepId}</c>, and its path locates the step's field.
    /// </summary>
    /// <param name="document">The Arazzo document whose steps are checked.</param>
    /// <param name="workspace">The workspace holding the source documents and their resolvers.</param>
    /// <returns>One error per reference that doesn't resolve; empty when all of them do.</returns>
    public static IReadOnlyList<ArazzoError> Validate(ArazzoDocument document, ArazzoWorkspace workspace) =>
        ResolveStepTargets(document, workspace)
            .Where(target => target.Status == ArazzoStepTargetStatus.Unresolved)
            .Select(target => new ArazzoError(target.Path, $"{target.WorkflowId}.{target.StepId}: {target.Message}"))
            .ToList();

    /// <summary>
    /// Resolves the target of every step in <paramref name="document"/> that has an <c>operationId</c>,
    /// <c>operationPath</c> or <c>channelPath</c>, returning what each resolved to. Comparing the results for two
    /// versions of the source documents shows which workflows an API change affects.
    /// </summary>
    /// <param name="document">The Arazzo document whose steps are resolved.</param>
    /// <param name="workspace">The workspace holding the source documents and their resolvers.</param>
    /// <returns>One entry per step with an API target, in document order. Steps that call a workflow are left out.</returns>
    public static IReadOnlyList<ArazzoStepTarget> ResolveStepTargets(ArazzoDocument document, ArazzoWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(workspace);

        var declared = document.SourceDescriptions.Select(source => source.Name).ToHashSet(StringComparer.Ordinal);
        // A bare operationId is looked up in the sources the document declares and the workspace has a document
        // for: a document registered under a name the document doesn't declare is not one of its sources.
        var context = new ResolutionContext(workspace, declared, workspace.SourceNames.Where(declared.Contains).ToList());
        var targets = new List<ArazzoStepTarget>();

        for (var w = 0; w < document.Workflows.Count; w++)
        {
            var workflow = document.Workflows[w];
            for (var s = 0; s < workflow.Steps.Count; s++)
            {
                var step = workflow.Steps[s];
                if (ResolveStep(step, context) is { } outcome)
                {
                    targets.Add(new ArazzoStepTarget(
                        workflow.WorkflowId, step.StepId, $"/workflows/{w}/steps/{s}/{outcome.Field}", outcome.Reference,
                        outcome.SourceName, outcome.Status, outcome.Target, outcome.Message));
                }
            }
        }

        return targets;
    }

    /// <summary>
    /// The sources <paramref name="document"/> declares that <paramref name="workspace"/> has no document for, so
    /// <see cref="Validate"/> skips the steps that target them.
    /// </summary>
    /// <param name="document">The Arazzo document.</param>
    /// <param name="workspace">The workspace holding the source documents.</param>
    /// <returns>The names of the unchecked sources, in declaration order.</returns>
    public static IReadOnlyList<string> UncheckedSources(ArazzoDocument document, ArazzoWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(workspace);

        return document.SourceDescriptions
            .Select(source => source.Name)
            .Where(name => !workspace.TryGetDocument(name, out _))
            .ToList();
    }

    private static Outcome? ResolveStep(ArazzoStep step, ResolutionContext context)
    {
        if (step.OperationPath is not null)
        {
            return ResolvePointerReference(step.OperationPath, "operationPath", context, context.Workspace.TryResolveOperationPath);
        }

        if (step.ChannelPath is not null)
        {
            return ResolvePointerReference(step.ChannelPath, "channelPath", context, context.Workspace.TryResolveChannelPath);
        }

        if (step.OperationId is not { } operationId)
        {
            return null;
        }

        const string field = "operationId";
        var qualified = QualifiedOperationIdPattern().Match(operationId);
        if (qualified.Success)
        {
            var sourceName = qualified.Groups[1].Value;
            if (Unavailable(sourceName, $"operationId '{operationId}'", field, operationId, context) is { } unavailable)
            {
                return unavailable;
            }

            return context.Workspace.TryResolveOperation(sourceName, qualified.Groups[2].Value, out var operation)
                ? Outcome.Resolved(field, operationId, sourceName, operation)
                : Outcome.Unresolved(field, operationId, sourceName, $"operationId '{operationId}' did not resolve.");
        }

        if (context.Registered.Count == 0)
        {
            return new Outcome(field, operationId, null, ArazzoStepTargetStatus.Skipped, null, null);
        }

        foreach (var source in context.Registered)
        {
            if (context.Workspace.TryResolveOperation(source, operationId, out var operation))
            {
                return Outcome.Resolved(field, operationId, source, operation);
            }
        }

        return Outcome.Unresolved(field, operationId, null,
            $"operationId '{operationId}' did not resolve against any registered source.");
    }

    private static Outcome ResolvePointerReference(
        string reference, string field, ResolutionContext context, TryResolvePointer resolve)
    {
        var match = SourceReferencePattern().Match(reference);
        if (!match.Success)
        {
            return Outcome.Unresolved(field, reference, null, $"{field} '{reference}' did not resolve.");
        }

        var sourceName = match.Groups[1].Value;
        if (Unavailable(sourceName, $"{field} '{reference}'", field, reference, context) is { } unavailable)
        {
            return unavailable;
        }

        // A resolver may report a match without returning the node, so the result decides, not the node.
        return resolve(sourceName, match.Groups[2].Value, out var target)
            ? Outcome.Resolved(field, reference, sourceName, target)
            : Outcome.Unresolved(field, reference, sourceName, $"{field} '{reference}' did not resolve.");
    }

    /// <summary>
    /// The outcome for a reference into <paramref name="sourceName"/> when it can't be resolved: unresolved for a
    /// source the document doesn't declare, even when the workspace has a document of that name; skipped for a
    /// declared source without a document. <see langword="null"/> when the reference can be resolved.
    /// </summary>
    private static Outcome? Unavailable(string sourceName, string description, string field, string reference, ResolutionContext context)
    {
        if (!context.Declared.Contains(sourceName))
        {
            return Outcome.Unresolved(field, reference, sourceName,
                $"{description} names source '{sourceName}', which the document doesn't declare.");
        }

        return context.Workspace.TryGetDocument(sourceName, out _)
            ? null
            : new Outcome(field, reference, sourceName, ArazzoStepTargetStatus.Skipped, null, null);
    }

    private delegate bool TryResolvePointer(string sourceName, string jsonPointer, out JsonNode? node);

    private sealed record ResolutionContext(ArazzoWorkspace Workspace, HashSet<string> Declared, List<string> Registered);

    private sealed record Outcome(
        string Field, string Reference, string? SourceName, ArazzoStepTargetStatus Status, JsonNode? Target, string? Message)
    {
        public static Outcome Resolved(string field, string reference, string sourceName, JsonNode? target) =>
            new(field, reference, sourceName, ArazzoStepTargetStatus.Resolved, target, null);

        public static Outcome Unresolved(string field, string reference, string? sourceName, string message) =>
            new(field, reference, sourceName, ArazzoStepTargetStatus.Unresolved, null, message);
    }
}
