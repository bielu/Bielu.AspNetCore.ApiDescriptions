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
    public static IReadOnlyList<ArazzoError> Validate(ArazzoDocument document, ArazzoWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(workspace);

        var declared = document.SourceDescriptions.Select(source => source.Name).ToHashSet(StringComparer.Ordinal);
        // A bare operationId is looked up in the sources the document declares and the workspace has a document
        // for: a document registered under a name the document doesn't declare is not one of its sources.
        var registered = workspace.SourceNames.Where(declared.Contains).ToList();
        var errors = new List<ArazzoError>();

        for (var w = 0; w < document.Workflows.Count; w++)
        {
            var workflow = document.Workflows[w];
            for (var s = 0; s < workflow.Steps.Count; s++)
            {
                var step = workflow.Steps[s];
                var context = new StepContext(
                    $"/workflows/{w}/steps/{s}", $"{workflow.WorkflowId}.{step.StepId}", workspace, declared, registered, errors);
                ValidateStep(step, context);
            }
        }

        return errors;
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

    private static void ValidateStep(ArazzoStep step, StepContext context)
    {
        if (step.OperationPath is not null)
        {
            ValidatePointerReference(step.OperationPath, "operationPath", context,
                (source, pointer) => context.Workspace.TryResolveOperationPath(source, pointer, out _));
            return;
        }

        if (step.ChannelPath is not null)
        {
            ValidatePointerReference(step.ChannelPath, "channelPath", context,
                (source, pointer) => context.Workspace.TryResolveChannelPath(source, pointer, out _));
            return;
        }

        if (step.OperationId is not { } operationId)
        {
            return;
        }

        var qualified = QualifiedOperationIdPattern().Match(operationId);
        if (qualified.Success)
        {
            var sourceName = qualified.Groups[1].Value;
            if (ShouldResolve(sourceName, $"operationId '{operationId}'", "operationId", context) &&
                !context.Workspace.TryResolveOperation(sourceName, qualified.Groups[2].Value, out _))
            {
                context.Add("operationId", $"operationId '{operationId}' did not resolve.");
            }

            return;
        }

        if (context.Registered.Count > 0 &&
            !context.Registered.Any(source => context.Workspace.TryResolveOperation(source, operationId, out _)))
        {
            context.Add("operationId", $"operationId '{operationId}' did not resolve against any registered source.");
        }
    }

    private static void ValidatePointerReference(
        string reference, string field, StepContext context, Func<string, string, bool> resolve)
    {
        var match = SourceReferencePattern().Match(reference);
        if (!match.Success)
        {
            context.Add(field, $"{field} '{reference}' did not resolve.");
            return;
        }

        var sourceName = match.Groups[1].Value;
        if (ShouldResolve(sourceName, $"{field} '{reference}'", field, context) &&
            !resolve(sourceName, match.Groups[2].Value))
        {
            context.Add(field, $"{field} '{reference}' did not resolve.");
        }
    }

    /// <summary>
    /// Whether a reference into <paramref name="sourceName"/> can be resolved: only when the document declares
    /// the source and the workspace has its document. An undeclared source is always reported, even when the
    /// workspace has a document of that name; a declared source without one is skipped.
    /// </summary>
    private static bool ShouldResolve(string sourceName, string reference, string field, StepContext context)
    {
        if (!context.Declared.Contains(sourceName))
        {
            context.Add(field, $"{reference} names source '{sourceName}', which the document doesn't declare.");
            return false;
        }

        return context.Workspace.TryGetDocument(sourceName, out _);
    }

    private sealed record StepContext(
        string Path,
        string Location,
        ArazzoWorkspace Workspace,
        HashSet<string> Declared,
        List<string> Registered,
        List<ArazzoError> Errors)
    {
        public void Add(string field, string message) => Errors.Add(new ArazzoError($"{Path}/{field}", $"{Location}: {message}"));
    }
}
