// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Models;
using Bielu.Arazzo.Readers;
using Bielu.Arazzo.Validation;
using Bielu.Cli.Shared;
using Bielu.Cli.Shared.Diagnostics;
using Bielu.Cli.Shared.Diff;

namespace Bielu.Arazzo.Cli.Commands;

/// <summary>
/// Reports which workflow steps of an Arazzo document an API change affects. Every step's target is resolved against
/// the source documents before the change (<c>--base</c>) and after it (<c>--head</c>): a target that no longer
/// resolves is breaking, and one whose definition changed (its parameters, request, responses, messages or payload
/// schemas, with local <c>$ref</c>s followed) is listed for review.
/// </summary>
internal sealed class ImpactCommandWorker
{
    private readonly ImpactCommandContext _context;
    private readonly ICliLogger _logger;

    public ImpactCommandWorker(
        ImpactCommandContext context,
        Action<string> writeInfo,
        Action<string> writeWarning,
        Action<string> writeError)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(writeInfo);
        ArgumentNullException.ThrowIfNull(writeWarning);
        ArgumentNullException.ThrowIfNull(writeError);

        _context = context;
        _logger = new DelegatingCliLogger(writeInfo, writeWarning, writeError);
    }

    public int Process()
    {
        if (ReadDocument() is not { } document)
        {
            return CliExitCode.Failure;
        }

        // A misspelled name would otherwise match no source description, leave both sides empty and report no
        // changes, whatever the files hold.
        var declared = document.SourceDescriptions.Select(source => source.Name).ToHashSet(StringComparer.Ordinal);
        var unknown = _context.BaseSources.Keys.Concat(_context.HeadSources.Keys)
            .Where(name => !declared.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (unknown.Count > 0)
        {
            foreach (var name in unknown)
            {
                _logger.Error($"Source '{name}' isn't declared in {_context.File}. Declared sources: {string.Join(", ", declared.Order(StringComparer.Ordinal))}.");
            }

            return CliExitCode.Failure;
        }

        foreach (var name in _context.BaseSources.Keys.Except(_context.HeadSources.Keys)
                     .Concat(_context.HeadSources.Keys.Except(_context.BaseSources.Keys)))
        {
            _logger.Warning($"Source '{name}' is given with only one of --base and --head, so its steps can't be compared.");
        }

        var loadErrors = new List<DiagnosticItem>();
        var baseWorkspace = SourceWorkspaceBuilder.Build(document, _context.BaseSources, null, loadErrors);
        var headWorkspace = SourceWorkspaceBuilder.Build(document, _context.HeadSources, null, loadErrors);
        if (loadErrors.Count > 0)
        {
            foreach (var error in loadErrors)
            {
                _logger.Error($"{error.Message} (at {error.Pointer})");
            }

            return CliExitCode.Failure;
        }

        var changes = Compare(
            ArazzoReferenceValidator.ResolveStepTargets(document, baseWorkspace), baseWorkspace,
            ArazzoReferenceValidator.ResolveStepTargets(document, headWorkspace), headWorkspace);

        DiffReportWriter.Write(changes, _context.Format, "Arazzo Workflow Impact Report", _logger);

        return _context.FailOnBreaking && DiffReportWriter.HasBreakingChanges(changes)
            ? CliExitCode.Failure
            : CliExitCode.Success;
    }

    /// <summary>
    /// Pairs each step's target before and after the change. Both lists come from the same Arazzo document, so they
    /// hold the same steps in the same order.
    /// </summary>
    internal List<DocumentChange> Compare(
        IReadOnlyList<ArazzoStepTarget> before, ArazzoWorkspace baseWorkspace,
        IReadOnlyList<ArazzoStepTarget> after, ArazzoWorkspace headWorkspace)
    {
        var changes = new List<DocumentChange>();
        for (var i = 0; i < before.Count; i++)
        {
            var (old, current) = (before[i], after[i]);
            var location = $"{current.WorkflowId}.{current.StepId}";

            switch (old.Status, current.Status)
            {
                case (ArazzoStepTargetStatus.Resolved, ArazzoStepTargetStatus.Unresolved):
                    changes.Add(new DocumentChange(location, $"{current.Message} It resolved before the change.", ChangeSeverity.Breaking));
                    break;
                case (ArazzoStepTargetStatus.Unresolved, ArazzoStepTargetStatus.Resolved):
                    changes.Add(new DocumentChange(location, $"'{current.Reference}' resolves after the change; it didn't before.", ChangeSeverity.NonBreaking));
                    break;
                case (ArazzoStepTargetStatus.Unresolved, ArazzoStepTargetStatus.Unresolved):
                    if (!string.Equals(_context.Format, "json", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.Warning($"{location}: '{current.Reference}' doesn't resolve before or after the change (see 'dotnet arazzo validate --source').");
                    }

                    break;
                case (ArazzoStepTargetStatus.Resolved, ArazzoStepTargetStatus.Resolved):
                    var (oldFingerprint, newFingerprint) = (Fingerprint(old, baseWorkspace), Fingerprint(current, headWorkspace));
                    if (!oldFingerprint.Complete || !newFingerprint.Complete)
                    {
                        // Cut short, two fingerprints can match although the definitions differ: say so, not "no change".
                        changes.Add(new DocumentChange(
                            location,
                            $"'{current.Reference}' targets a {Kind(current)} in source '{current.SourceName}' that can't be compared in full: nested too deeply, or behind a reference that can't be followed. Review the step against it.",
                            ChangeSeverity.NonBreaking));
                    }
                    else if (!string.Equals(oldFingerprint.Value, newFingerprint.Value, StringComparison.Ordinal))
                    {
                        changes.Add(new DocumentChange(
                            location,
                            $"The {Kind(current)} '{current.Reference}' targets changed in source '{current.SourceName}'. Review the step's parameters, request body, success criteria and outputs.",
                            ChangeSeverity.NonBreaking));
                    }

                    break;
            }
        }

        return changes;
    }

    private static string Kind(ArazzoStepTarget target) =>
        target.Path.EndsWith("/channelPath", StringComparison.Ordinal) ? "channel" : "operation";

    private static StepTargetFingerprint.Result Fingerprint(ArazzoStepTarget target, ArazzoWorkspace workspace) =>
        target.SourceName is not null && workspace.TryGetDocument(target.SourceName, out var document) &&
        document is System.Text.Json.Nodes.JsonNode root
            ? StepTargetFingerprint.Of(target.Target, root)
            : new StepTargetFingerprint.Result(string.Empty, Complete: true);

    private ArazzoDocument? ReadDocument()
    {
        var path = _context.File!;
        if (!File.Exists(path))
        {
            _logger.Error($"File not found: {path}");
            return null;
        }

        string content;
        try
        {
            content = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error($"Failed to read input: {ex.Message}");
            return null;
        }

        var result = ArazzoStringReader.Read(content);
        if (result.Document is null)
        {
            foreach (var error in result.Diagnostics.Errors)
            {
                _logger.Error($"{path}: {error.Message} (at {error.Path})");
            }

            return null;
        }

        return result.Document;
    }
}
