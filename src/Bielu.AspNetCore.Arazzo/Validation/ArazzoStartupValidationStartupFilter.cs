using System.Text.RegularExpressions;
using Bielu.Arazzo;
using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bielu.AspNetCore.Arazzo.Validation;

/// <summary>
/// Resolves every workflow step's <c>operationId</c>/<c>operationPath</c>/<c>channelPath</c> against the live,
/// self-wired AsyncAPI/OpenAPI documents once at app startup, for every document that opted in via
/// <see cref="ArazzoOptions.ValidateSourceReferencesOnStartup"/>.
/// </summary>
/// <remarks>
/// Only steps that target a self-wired source are checked. A source added by URL alone
/// (<see cref="ArazzoOptions.AddSourceDescription"/>), such as another service's OpenAPI document, has no
/// in-process document to resolve against, so its steps are skipped; a step naming a source the document
/// doesn't declare at all is still reported.
/// </remarks>
/// <remarks>
/// Implemented as an <see cref="IStartupFilter"/> rather than an <see cref="Microsoft.Extensions.Hosting.IHostedService"/>:
/// the endpoints/ApiDescriptions this validation depends on are only registered once the app's own
/// <c>Configure</c> delegate runs, which — for the Generic Host + Kestrel/TestServer pipeline — happens
/// inside the framework's own hosted service, strictly *after* user-registered <c>IHostedService</c>s have
/// already run. An <see cref="IStartupFilter"/> instead runs as part of building the request pipeline
/// itself, guaranteeing the app's endpoints already exist, and — unlike exceptions thrown from
/// <c>IHostApplicationLifetime.ApplicationStarted</c> callbacks, which the host only logs — an exception
/// thrown here genuinely fails <c>host.StartAsync()</c>.
/// </remarks>
internal sealed partial class ArazzoStartupValidationStartupFilter(
    IServiceProvider serviceProvider,
    IEnumerable<NamedArazzoDocument> documents,
    IOptionsMonitor<ArazzoOptions> optionsMonitor) : IStartupFilter
{
    [GeneratedRegex(@"^\{\$sourceDescriptions\.([A-Za-z0-9_-]+)\.url\}#(.*)$")]
    private static partial Regex SourceReferencePattern();

    [GeneratedRegex(@"^\$sourceDescriptions\.([A-Za-z0-9_-]+)\.(.+)$")]
    private static partial Regex QualifiedOperationIdPattern();

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        ValidateAsync(CancellationToken.None).GetAwaiter().GetResult();
    };

    private async Task ValidateAsync(CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        foreach (var documentName in documents.Select(d => d.DocumentName).Distinct(StringComparer.Ordinal))
        {
            var options = optionsMonitor.Get(documentName);
            if (!options.ValidateSourceReferencesOnStartup || options.SourceWirings.Count == 0)
            {
                continue;
            }

            // Document generation runs arbitrary user-supplied transformers (AddDocumentTransformer /
            // AddOperationTransformer). Cancellation is cooperative, so a transformer that ignores the
            // token could still block indefinitely if we only *asked* it to stop — WaitAsync enforces the
            // deadline at the waiter instead, regardless of whether the awaited work ever observes it.
            try
            {
                await ValidateDocumentAsync(documentName, options, errors, cancellationToken)
                    .WaitAsync(options.StartupValidationTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                errors.Add(
                    $"{documentName}: startup validation did not complete within {options.StartupValidationTimeout} " +
                    "(a self-wired document provider or transformer may be hanging).");
            }
        }

        if (errors.Count > 0)
        {
            throw new ArazzoStartupValidationException(errors);
        }
    }

    private async Task ValidateDocumentAsync(
        string documentName, ArazzoOptions options, List<string> errors, CancellationToken cancellationToken)
    {
        var arazzoDocument = await serviceProvider
            .GetRequiredKeyedService<IArazzoDocumentProvider>(documentName)
            .GetArazzoDocumentAsync(cancellationToken);
        var (workspace, failedSourceNames) = await serviceProvider
            .GetRequiredKeyedService<ArazzoWorkspaceFactory>(documentName)
            .CreateAsync(errors, cancellationToken);

        var sources = new StepSources(
            options.SourceWirings,
            options.SourceWirings.Select(wiring => wiring.SourceName).ToHashSet(StringComparer.Ordinal),
            arazzoDocument.SourceDescriptions.Select(source => source.Name).ToHashSet(StringComparer.Ordinal),
            failedSourceNames);

        foreach (var workflow in arazzoDocument.Workflows)
        {
            foreach (var step in workflow.Steps)
            {
                ValidateStep(documentName, workflow, step, workspace, sources, errors);
            }
        }
    }

    private static void ValidateStep(
        string documentName,
        ArazzoWorkflow workflow,
        ArazzoStep step,
        ArazzoWorkspace workspace,
        StepSources sources,
        List<string> errors)
    {
        var location = $"{documentName}:{workflow.WorkflowId}.{step.StepId}";

        if (step.OperationPath is not null)
        {
            if (TryParseSourceReference(step.OperationPath, out var sourceName, out var pointer))
            {
                if (ShouldResolve(sources, sourceName, location, $"operationPath '{step.OperationPath}'", errors) &&
                    !workspace.TryResolveOperationPath(sourceName, pointer, out _))
                {
                    errors.Add($"{location}: operationPath '{step.OperationPath}' did not resolve.");
                }
            }
            else
            {
                errors.Add($"{location}: operationPath '{step.OperationPath}' did not resolve.");
            }

            return;
        }

        if (step.ChannelPath is not null)
        {
            if (TryParseSourceReference(step.ChannelPath, out var sourceName, out var pointer))
            {
                if (ShouldResolve(sources, sourceName, location, $"channelPath '{step.ChannelPath}'", errors) &&
                    !workspace.TryResolveChannelPath(sourceName, pointer, out _))
                {
                    errors.Add($"{location}: channelPath '{step.ChannelPath}' did not resolve.");
                }
            }
            else
            {
                errors.Add($"{location}: channelPath '{step.ChannelPath}' did not resolve.");
            }

            return;
        }

        if (step.OperationId is not null)
        {
            // A qualified id ($sourceDescriptions.<name>.<operationId>) names its source: resolve the bare id there.
            var qualified = QualifiedOperationIdPattern().Match(step.OperationId);
            if (qualified.Success)
            {
                var sourceName = qualified.Groups[1].Value;
                if (ShouldResolve(sources, sourceName, location, $"operationId '{step.OperationId}'", errors) &&
                    !workspace.TryResolveOperation(sourceName, qualified.Groups[2].Value, out _))
                {
                    errors.Add($"{location}: operationId '{step.OperationId}' did not resolve.");
                }

                return;
            }

            var candidateWirings = sources.Wirings
                .Where(wiring => !sources.FailedSourceNames.Contains(wiring.SourceName)).ToList();
            if (candidateWirings.Count > 0 &&
                !candidateWirings.Any(wiring =>
                    workspace.TryResolveOperation(wiring.SourceName, step.OperationId, out _)))
            {
                errors.Add(
                    $"{location}: operationId '{step.OperationId}' did not resolve against any registered source.");
            }
        }
    }

    /// <summary>
    /// Whether a step's reference into <paramref name="sourceName"/> can be resolved here: only a self-wired
    /// source whose document loaded. A source added by URL alone can't be fetched at startup, so it's skipped;
    /// a source whose provider failed already has a root-cause error from ArazzoWorkspaceFactory.CreateAsync;
    /// a source the document doesn't declare is reported.
    /// </summary>
    private static bool ShouldResolve(
        StepSources sources, string sourceName, string location, string reference, List<string> errors)
    {
        if (sources.WiredSourceNames.Contains(sourceName))
        {
            return !sources.FailedSourceNames.Contains(sourceName);
        }

        if (!sources.DeclaredSourceNames.Contains(sourceName))
        {
            errors.Add($"{location}: {reference} names source '{sourceName}', which the document doesn't declare.");
        }

        return false;
    }

    private sealed record StepSources(
        IReadOnlyList<ArazzoOptions.SourceWiring> Wirings,
        HashSet<string> WiredSourceNames,
        HashSet<string> DeclaredSourceNames,
        HashSet<string> FailedSourceNames);

    private static bool TryParseSourceReference(string reference, out string sourceName, out string jsonPointer)
    {
        var match = SourceReferencePattern().Match(reference);
        if (!match.Success)
        {
            sourceName = string.Empty;
            jsonPointer = string.Empty;
            return false;
        }

        sourceName = match.Groups[1].Value;
        jsonPointer = match.Groups[2].Value;
        return true;
    }
}
