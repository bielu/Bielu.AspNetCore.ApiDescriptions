using Bielu.Arazzo.Validation;
using Bielu.AspNetCore.Arazzo.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bielu.AspNetCore.Arazzo.Validation;

/// <summary>
/// Resolves every workflow step's <c>operationId</c>/<c>operationPath</c>/<c>channelPath</c> against the live,
/// self-wired AsyncAPI/OpenAPI documents once at app startup, for every document that opted in via
/// <see cref="ArazzoOptions.ValidateSourceReferencesOnStartup"/>. The checks themselves are
/// <see cref="ArazzoReferenceValidator"/>'s, which <c>dotnet-arazzo validate --source</c> runs against files.
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
internal sealed class ArazzoStartupValidationStartupFilter(
    IServiceProvider serviceProvider,
    IEnumerable<NamedArazzoDocument> documents,
    IOptionsMonitor<ArazzoOptions> optionsMonitor) : IStartupFilter
{
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
                await ValidateDocumentAsync(documentName, errors, cancellationToken)
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

    private async Task ValidateDocumentAsync(string documentName, List<string> errors, CancellationToken cancellationToken)
    {
        var arazzoDocument = await serviceProvider
            .GetRequiredKeyedService<IArazzoDocumentProvider>(documentName)
            .GetArazzoDocumentAsync(cancellationToken);
        var workspace = await serviceProvider
            .GetRequiredKeyedService<ArazzoWorkspaceFactory>(documentName)
            .CreateAsync(errors, cancellationToken);

        // A source whose provider failed has no document in the workspace, so the validator skips its steps:
        // ArazzoWorkspaceFactory has already reported the root cause.
        errors.AddRange(ArazzoReferenceValidator.Validate(arazzoDocument, workspace)
            .Select(error => $"{documentName}:{error.Message}"));
    }
}
