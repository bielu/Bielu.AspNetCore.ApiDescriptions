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

        foreach (var workflow in arazzoDocument.Workflows)
        {
            foreach (var step in workflow.Steps)
            {
                ValidateStep(documentName, workflow, step, workspace, options.SourceWirings, failedSourceNames, errors);
            }
        }
    }

    private static void ValidateStep(
        string documentName,
        ArazzoWorkflow workflow,
        ArazzoStep step,
        ArazzoWorkspace workspace,
        IReadOnlyList<ArazzoOptions.SourceWiring> wirings,
        HashSet<string> failedSourceNames,
        List<string> errors)
    {
        var location = $"{documentName}:{workflow.WorkflowId}.{step.StepId}";

        if (step.OperationPath is not null)
        {
            if (TryParseSourceReference(step.OperationPath, out var sourceName, out var pointer))
            {
                // A missing provider for this source already produced a root-cause error in
                // ArazzoWorkspaceFactory.CreateAsync; don't also report every step that references it.
                if (!failedSourceNames.Contains(sourceName) &&
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
                if (!failedSourceNames.Contains(sourceName) &&
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
            var candidateWirings = wirings.Where(wiring => !failedSourceNames.Contains(wiring.SourceName)).ToList();
            if (candidateWirings.Count > 0 &&
                !candidateWirings.Any(wiring =>
                    workspace.TryResolveOperation(wiring.SourceName, step.OperationId, out _)))
            {
                errors.Add(
                    $"{location}: operationId '{step.OperationId}' did not resolve against any registered source.");
            }
        }
    }

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
