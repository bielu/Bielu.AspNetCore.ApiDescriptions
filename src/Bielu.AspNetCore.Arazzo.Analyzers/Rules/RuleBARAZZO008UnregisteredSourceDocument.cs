using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.AspNetCore.Arazzo.Analyzers.Rules;

/// <summary>
/// Reports <c>AddAsyncApiSource</c>/<c>AddOpenApiSource</c> pointing at a document name the project never registers
/// with <c>AddAsyncApi</c>/<c>AddOpenApi</c>.
/// </summary>
/// <remarks>
/// Only reported when the project registers at least one document of that kind and every registration's name is a
/// constant, so a document registered from another assembly, or by <c>AddAsyncApiForApiVersions</c> (whose names
/// come from API versions), doesn't cause a false report. That's the same trade-off BASYNC004 makes.
/// </remarks>
internal sealed class RuleBARAZZO008UnregisteredSourceDocument
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BARAZZO008",
        title: "Source points at an unregistered document",
        messageFormat: "'{0}' points at the {1} document '{2}', which this project doesn't register with '{3}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "AddAsyncApiSource and AddOpenApiSource self-wire a source description against a document this app serves. When no document of that name is registered, the advertised URL returns 404 and startup source validation fails.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private readonly DocumentKind _asyncApi = new("AsyncAPI", "AddAsyncApi", "AddAsyncApiSource", "asyncApiDocumentName");
    private readonly DocumentKind _openApi = new("OpenAPI", "AddOpenApi", "AddOpenApiSource", "openApiDocumentName");

    public static void Register(AnalysisContext context) =>
        context.RegisterCompilationStartAction(start =>
        {
            var rule = new RuleBARAZZO008UnregisteredSourceDocument();
            start.RegisterOperationAction(rule.Collect, OperationKind.Invocation);
            start.RegisterCompilationEndAction(rule.Report);
        });

    private void Collect(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        switch (method.ContainingType?.ToDisplayString())
        {
            case "Bielu.AspNetCore.AsyncApi.Extensions.AsyncApiServiceCollectionExtensions" when method.Name == "AddAsyncApi":
                _asyncApi.Registered(invocation);
                break;
            case "Microsoft.Extensions.DependencyInjection.OpenApiServiceCollectionExtensions" when method.Name == "AddOpenApi":
                _openApi.Registered(invocation);
                break;
            case "Bielu.AspNetCore.AsyncApi.Versioning.AsyncApiVersioningServiceCollectionExtensions" when method.Name == "AddAsyncApiForApiVersions":
                _asyncApi.HasUnknownNames();
                break;
            case ArazzoSymbols.OptionsType:
                _asyncApi.Referenced(invocation);
                _openApi.Referenced(invocation);
                break;
        }
    }

    private void Report(CompilationAnalysisContext context)
    {
        _asyncApi.Report(context);
        _openApi.Report(context);
    }

    private sealed class DocumentKind(string kind, string registerMethod, string sourceMethod, string documentNameParameter)
    {
        private readonly ConcurrentBag<string> _registered = [];
        private readonly ConcurrentBag<(string Name, Location Location)> _referenced = [];
        private volatile bool _hasUnknownNames;

        public void HasUnknownNames() => _hasUnknownNames = true;

        // Both libraries default the document name to "v1" and lowercase it.
        public void Registered(IInvocationOperation invocation)
        {
            if (ArazzoSymbols.Argument(invocation, "documentName") is null)
            {
                _registered.Add(ArazzoSymbols.DefaultDocumentName);
            }
            else if (ArazzoSymbols.ConstantString(invocation, "documentName") is { } name)
            {
                _registered.Add(name.ToLowerInvariant());
            }
            else
            {
                _hasUnknownNames = true;
            }
        }

        public void Referenced(IInvocationOperation invocation)
        {
            if (invocation.TargetMethod.Name == sourceMethod
                && ArazzoSymbols.Argument(invocation, documentNameParameter)?.Value is { ConstantValue: { HasValue: true, Value: string name } } value)
            {
                _referenced.Add((name, value.Syntax.GetLocation()));
            }
        }

        public void Report(CompilationAnalysisContext context)
        {
            var registered = new HashSet<string>(_registered, StringComparer.OrdinalIgnoreCase);
            if (_hasUnknownNames || registered.Count == 0)
            {
                return;
            }

            foreach (var (name, location) in _referenced.Where(r => !registered.Contains(r.Name)))
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, sourceMethod, kind, name, registerMethod));
            }
        }
    }
}
