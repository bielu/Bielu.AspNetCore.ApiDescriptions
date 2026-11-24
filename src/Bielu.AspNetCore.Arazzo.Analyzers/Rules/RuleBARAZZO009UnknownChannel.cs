using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.AspNetCore.Arazzo.Analyzers.Rules;

/// <summary>
/// Reports a <c>Channel(sourceName, channelName, action)</c> step whose channel no <c>[Channel]</c> attribute in the
/// project or its references declares, when the source is this app's own AsyncAPI document
/// (<c>AddAsyncApiSource</c>). A renamed or removed channel then fails the build rather than startup validation.
/// </summary>
/// <remarks>
/// <para>
/// A channel's key in the generated document is its <c>[Channel]</c> name with every character outside
/// <c>[A-Za-z0-9._-]</c> removed, so a step that uses the declared name instead of the key is reported too, with
/// the key to use.
/// </para>
/// <para>
/// Channels can also be added at runtime, which the analyzer can't see. A step passing <c>dynamic: true</c> is
/// skipped, and so is the whole check when the project calls <c>AddWolverine()</c>, when no <c>[Channel]</c> is
/// found at all, or when a referenced assembly uses the AsyncAPI attributes but exposes no channel the analyzer
/// can read (metadata leaves out non-public members, so its channels may be on internal or private methods).
/// </para>
/// </remarks>
internal sealed class RuleBARAZZO009UnknownChannel
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BARAZZO009",
        title: "Channel step targets an undeclared channel",
        messageFormat: "No [Channel] declares the channel '{0}' of source '{1}'{2}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A Channel step points at /channels/<name> in the app's own AsyncAPI document, where the key is the [Channel] name without the characters outside [A-Za-z0-9._-]. A step naming a channel no [Channel] declares fails startup validation, or ships a workflow that points at nothing. Pass dynamic: true for a channel a runtime mapping such as Wolverine routing creates.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private const string ChannelAttributeType = "Bielu.AspNetCore.AsyncApi.Attributes.ChannelAttribute";
    private const string AttributesAssembly = "Bielu.AspNetCore.AsyncApi.Attributes";
    private const string WolverineExtensionsType = "Bielu.AspNetCore.AsyncApi.Wolverine.WolverineAsyncApiOptionsExtensions";

    private static readonly Regex KeyDisallowedCharacters = new(@"[^a-zA-Z0-9\.\-_]", RegexOptions.Compiled);

    private readonly ConcurrentDictionary<string, string> _declaredNamesByKey = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<string> _selfWiredSources = [];
    private readonly ConcurrentBag<(string Source, string Channel, Location Location)> _steps = [];
    private volatile bool _channelsCreatedAtRuntime;
    private volatile bool _channelsHidden;

    public static void Register(AnalysisContext context) =>
        context.RegisterCompilationStartAction(start =>
        {
            var channelAttribute = start.Compilation.GetTypeByMetadataName(ChannelAttributeType);
            if (channelAttribute is null)
            {
                return;
            }

            var rule = new RuleBARAZZO009UnknownChannel();
            rule.CollectReferencedChannels(start.Compilation, channelAttribute);
            start.RegisterSymbolAction(symbol => rule.CollectAttributes(symbol.Symbol, channelAttribute), SymbolKind.NamedType, SymbolKind.Method);
            start.RegisterOperationAction(rule.Collect, OperationKind.Invocation);
            start.RegisterCompilationEndAction(rule.Report);
        });

    /// <summary>The key the AsyncAPI generator gives a channel: <c>AsyncApiNamingHelper.SanitizeKey</c>.</summary>
    internal static string ChannelKey(string name) => KeyDisallowedCharacters.Replace(name, string.Empty);

    private void CollectAttributes(ISymbol symbol, INamedTypeSymbol channelAttribute)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, channelAttribute)
                && attribute.ConstructorArguments.Length > 0
                && attribute.ConstructorArguments[0].Value is string name)
            {
                _declaredNamesByKey.TryAdd(ChannelKey(name), name);
            }
        }
    }

    /// <summary>
    /// Reads the <c>[Channel]</c>s of referenced assemblies, such as a library holding message contracts. Metadata
    /// leaves out non-public members, so an assembly that uses the attributes without a visible channel makes the
    /// declared set incomplete.
    /// </summary>
    private void CollectReferencedChannels(Compilation compilation, INamedTypeSymbol channelAttribute)
    {
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (assembly.Name.StartsWith("Bielu.AspNetCore.", StringComparison.Ordinal)
                || !assembly.Modules.Any(module => module.ReferencedAssemblySymbols.Any(r => r.Name == AttributesAssembly)))
            {
                continue;
            }

            var before = _declaredNamesByKey.Count;
            CollectMembers(assembly.GlobalNamespace, channelAttribute);
            if (_declaredNamesByKey.Count == before)
            {
                _channelsHidden = true;
            }
        }
    }

    private void CollectMembers(INamespaceOrTypeSymbol container, INamedTypeSymbol channelAttribute)
    {
        foreach (var member in container.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol ns:
                    CollectMembers(ns, channelAttribute);
                    break;
                case INamedTypeSymbol type:
                    CollectAttributes(type, channelAttribute);
                    foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
                    {
                        CollectAttributes(method, channelAttribute);
                    }

                    // Nested types.
                    CollectMembers(type, channelAttribute);
                    break;
            }
        }
    }

    private void Collect(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (method.ContainingType?.ToDisplayString() == WolverineExtensionsType && method.Name == "AddWolverine")
        {
            _channelsCreatedAtRuntime = true;
        }
        else if (method.Name == "AddAsyncApiSource" && ArazzoSymbols.IsOn(invocation, ArazzoSymbols.OptionsType)
                 && ArazzoSymbols.ConstantString(invocation, "sourceName") is { } sourceName)
        {
            _selfWiredSources.Add(sourceName);
        }
        else if (method.Name == "Channel" && ArazzoSymbols.IsOn(invocation, ArazzoSymbols.StepBuilderType)
                 && !IsDynamic(invocation)
                 && ArazzoSymbols.ConstantString(invocation, "sourceName") is { } source
                 && ArazzoSymbols.Argument(invocation, "channelName")?.Value is { ConstantValue: { HasValue: true, Value: string channel } } channelValue)
        {
            _steps.Add((source, channel, channelValue.Syntax.GetLocation()));
        }
    }

    // dynamic: true, or anything that isn't a constant false, since the analyzer can't tell.
    private static bool IsDynamic(IInvocationOperation invocation) =>
        ArazzoSymbols.Argument(invocation, "dynamic")?.Value is { } value
        && value.ConstantValue is not { HasValue: true, Value: false };

    private void Report(CompilationAnalysisContext context)
    {
        if (_channelsCreatedAtRuntime || _channelsHidden || _declaredNamesByKey.IsEmpty)
        {
            return;
        }

        var selfWired = new HashSet<string>(_selfWiredSources, StringComparer.Ordinal);
        foreach (var (source, channel, location) in _steps)
        {
            if (!selfWired.Contains(source) || _declaredNamesByKey.ContainsKey(channel))
            {
                continue;
            }

            var hint = _declaredNamesByKey.TryGetValue(ChannelKey(channel), out var declared)
                ? $"; the document keys the channel declared as '{declared}' as '{ChannelKey(channel)}', so use that"
                : "; if a runtime mapping such as Wolverine routing or a document transformer creates it, pass dynamic: true";
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, channel, source, hint));
        }
    }
}
