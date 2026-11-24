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
/// Reports a <c>Channel(sourceName, channelName, action)</c> step whose channel the AsyncAPI document behind the
/// source doesn't declare, when the source is this app's own AsyncAPI document (<c>AddAsyncApiSource</c>). A
/// renamed or removed channel then fails the build rather than startup validation.
/// </summary>
/// <remarks>
/// <para>
/// The source name is resolved in the Arazzo document the step belongs to, so a name that one document binds to
/// this app's AsyncAPI document and another to a partner's URL is only checked in the first. The channels checked
/// against are the ones runtime discovery puts in the bound AsyncAPI document: a <c>[Channel]</c> on a type marked
/// <c>[AsyncApi]</c> for that document (or for every document), or on one of its public methods.
/// </para>
/// <para>
/// A channel's key in the generated document is its <c>[Channel]</c> name with every character outside
/// <c>[A-Za-z0-9._-]</c> removed, so a step that uses the declared name instead of the key is reported too, with
/// the key to use.
/// </para>
/// <para>
/// Channels can also be added at runtime, which the analyzer can't see. A step passing <c>dynamic: true</c> is
/// skipped, and so is the whole check when the project calls <c>AddWolverine()</c>, when no channel is found at
/// all, or when a referenced assembly uses the AsyncAPI attributes but exposes no channel the analyzer can read
/// (metadata leaves out non-public members, so its channels may be on internal or private methods). A step is also
/// skipped when the analyzer can't tell which AsyncAPI document its source is bound to: the Arazzo or AsyncAPI
/// document name isn't a constant, or the step or the source is declared outside an <c>AddArazzo</c> lambda.
/// </para>
/// </remarks>
internal sealed class RuleBARAZZO009UnknownChannel
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BARAZZO009",
        title: "Channel step targets an undeclared channel",
        messageFormat: "No [Channel] of the AsyncAPI document '{0}' declares the channel '{1}' of source '{2}'{3}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A Channel step points at /channels/<name> in the app's own AsyncAPI document, where the key is the [Channel] name without the characters outside [A-Za-z0-9._-]. Only a [Channel] on an [AsyncApi] type of that document, or on one of its public methods, ends up there. A step naming any other channel fails startup validation, or ships a workflow that points at nothing. Pass dynamic: true for a channel a runtime mapping such as Wolverine routing creates.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private const string AsyncApiAttributeType = "Bielu.AspNetCore.AsyncApi.Attributes.AsyncApiAttribute";
    private const string ChannelAttributeType = "Bielu.AspNetCore.AsyncApi.Attributes.ChannelAttribute";
    private const string AttributesAssembly = "Bielu.AspNetCore.AsyncApi.Attributes";
    private const string WolverineExtensionsType = "Bielu.AspNetCore.AsyncApi.Wolverine.WolverineAsyncApiOptionsExtensions";

    private static readonly Regex KeyDisallowedCharacters = new(@"[^a-zA-Z0-9\.\-_]", RegexOptions.Compiled);

    private readonly INamedTypeSymbol _asyncApiAttribute;
    private readonly INamedTypeSymbol _channelAttribute;

    // Channel key -> declared name: for [AsyncApi] types without a document name, which every document gets, and
    // for the ones naming a document, by that (lowercased) name.
    private readonly ConcurrentDictionary<string, string> _channelsOfEveryDocument = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _channelsByDocument = new(StringComparer.Ordinal);

    private readonly ConcurrentBag<SelfWiredSource> _sources = [];
    private readonly ConcurrentBag<ChannelStep> _steps = [];
    private volatile bool _channelsCreatedAtRuntime;
    private volatile bool _channelsHidden;

    private RuleBARAZZO009UnknownChannel(INamedTypeSymbol asyncApiAttribute, INamedTypeSymbol channelAttribute)
    {
        _asyncApiAttribute = asyncApiAttribute;
        _channelAttribute = channelAttribute;
    }

    public static void Register(AnalysisContext context) =>
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(AsyncApiAttributeType) is not { } asyncApiAttribute
                || start.Compilation.GetTypeByMetadataName(ChannelAttributeType) is not { } channelAttribute)
            {
                return;
            }

            var rule = new RuleBARAZZO009UnknownChannel(asyncApiAttribute, channelAttribute);
            rule.CollectReferencedChannels(start.Compilation);
            start.RegisterSymbolAction(symbol => rule.CollectType((INamedTypeSymbol)symbol.Symbol), SymbolKind.NamedType);
            start.RegisterOperationAction(rule.Collect, OperationKind.Invocation);
            start.RegisterCompilationEndAction(rule.Report);
        });

    /// <summary>The key the AsyncAPI generator gives a channel: <c>AsyncApiNamingHelper.SanitizeKey</c>.</summary>
    internal static string ChannelKey(string name) => KeyDisallowedCharacters.Replace(name, string.Empty);

    /// <summary>
    /// Records the channels runtime discovery (<c>ReflectionAsyncApiMetadataProvider</c>) takes from
    /// <paramref name="type"/>: none unless the type, or a base class, is marked <c>[AsyncApi]</c>; then the type's
    /// own <c>[Channel]</c> (or a base class's) and those of its public methods.
    /// </summary>
    private void CollectType(INamedTypeSymbol type)
    {
        if (Attribute(type, _asyncApiAttribute, inherit: true) is not { } asyncApi)
        {
            return;
        }

        var channels = asyncApi.ConstructorArguments.FirstOrDefault().Value is string documentName
            ? _channelsByDocument.GetOrAdd(documentName.ToLowerInvariant(), _ => new ConcurrentDictionary<string, string>(StringComparer.Ordinal))
            : _channelsOfEveryDocument;

        Add(channels, Attribute(type, _channelAttribute, inherit: true));
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>()
                     .Where(m => m.MethodKind == MethodKind.Ordinary && m.DeclaredAccessibility == Accessibility.Public))
        {
            Add(channels, Attribute(method, _channelAttribute, inherit: false));
        }
    }

    private static void Add(ConcurrentDictionary<string, string> channels, AttributeData? channel)
    {
        if (channel?.ConstructorArguments.FirstOrDefault().Value is string name)
        {
            channels.TryAdd(ChannelKey(name), name);
        }
    }

    // The attribute on the symbol, or with inherit, on the nearest base class carrying it, as GetCustomAttribute does.
    private static AttributeData? Attribute(ISymbol symbol, INamedTypeSymbol attributeType, bool inherit)
    {
        for (ISymbol? current = symbol; current is not null; current = inherit ? (current as INamedTypeSymbol)?.BaseType : null)
        {
            var attribute = current.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));
            if (attribute is not null)
            {
                return attribute;
            }
        }

        return null;
    }

    private int ChannelCount => _channelsOfEveryDocument.Count + _channelsByDocument.Values.Sum(c => c.Count);

    /// <summary>
    /// Reads the <c>[Channel]</c>s of referenced assemblies, such as a library holding message contracts. Metadata
    /// leaves out non-public members, so an assembly that uses the attributes without a visible channel makes the
    /// declared set incomplete.
    /// </summary>
    private void CollectReferencedChannels(Compilation compilation)
    {
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (assembly.Name.StartsWith("Bielu.AspNetCore.", StringComparison.Ordinal)
                || !assembly.Modules.Any(module => module.ReferencedAssemblySymbols.Any(r => r.Name == AttributesAssembly)))
            {
                continue;
            }

            var before = ChannelCount;
            CollectMembers(assembly.GlobalNamespace);
            if (ChannelCount == before)
            {
                _channelsHidden = true;
            }
        }
    }

    private void CollectMembers(INamespaceOrTypeSymbol container)
    {
        foreach (var member in container.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol ns:
                    CollectMembers(ns);
                    break;
                case INamedTypeSymbol type:
                    CollectType(type);

                    // Nested types.
                    CollectMembers(type);
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
            _sources.Add(new SelfWiredSource(
                ArazzoDocumentName(invocation),
                sourceName,
                ArazzoSymbols.ConstantString(invocation, "asyncApiDocumentName")?.ToLowerInvariant()));
        }
        else if (method.Name == "Channel" && ArazzoSymbols.IsOn(invocation, ArazzoSymbols.StepBuilderType)
                 && !IsDynamic(invocation)
                 && ArazzoSymbols.ConstantString(invocation, "sourceName") is { } source
                 && ArazzoSymbols.Argument(invocation, "channelName")?.Value is { ConstantValue: { HasValue: true, Value: string channel } } channelValue)
        {
            _steps.Add(new ChannelStep(ArazzoDocumentName(invocation), source, channel, channelValue.Syntax.GetLocation()));
        }
    }

    // The Arazzo document whose AddArazzo lambda holds the call, or null when it's elsewhere or not a constant.
    private static string? ArazzoDocumentName(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IInvocationOperation invocation && ArazzoSymbols.IsAddArazzo(invocation))
            {
                return ArazzoSymbols.DocumentName(invocation);
            }
        }

        return null;
    }

    // dynamic: true, or anything that isn't a constant false, since the analyzer can't tell.
    private static bool IsDynamic(IInvocationOperation invocation) =>
        ArazzoSymbols.Argument(invocation, "dynamic")?.Value is { } value
        && value.ConstantValue is not { HasValue: true, Value: false };

    private void Report(CompilationAnalysisContext context)
    {
        if (_channelsCreatedAtRuntime || _channelsHidden || ChannelCount == 0)
        {
            return;
        }

        var sources = _sources.ToList();
        foreach (var step in _steps)
        {
            if (BoundAsyncApiDocument(step, sources) is not { } documentName)
            {
                continue;
            }

            var channels = new Dictionary<string, string>(_channelsOfEveryDocument, StringComparer.Ordinal);
            if (_channelsByDocument.TryGetValue(documentName, out var documentChannels))
            {
                foreach (var channel in documentChannels.Where(c => !channels.ContainsKey(c.Key)))
                {
                    channels.Add(channel.Key, channel.Value);
                }
            }

            if (channels.ContainsKey(step.Channel))
            {
                continue;
            }

            var hint = channels.TryGetValue(ChannelKey(step.Channel), out var declared)
                ? $"; the document keys the channel declared as '{declared}' as '{ChannelKey(step.Channel)}', so use that"
                : "; if a runtime mapping such as Wolverine routing or a document transformer creates it, pass dynamic: true";
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, step.Location, documentName, step.Channel, step.Source, hint));
        }
    }

    /// <summary>
    /// The AsyncAPI document the step's source is bound to in the step's Arazzo document, or <see langword="null"/>
    /// when the source isn't self-wired there (a URL, or undeclared, which is BARAZZO006's job) or the binding can't
    /// be told.
    /// </summary>
    private static string? BoundAsyncApiDocument(ChannelStep step, List<SelfWiredSource> sources)
    {
        if (step.ArazzoDocument is null
            // A source registered where the analyzer can't tell the Arazzo document might belong to this one.
            || sources.Any(s => s.ArazzoDocument is null && s.Name == step.Source))
        {
            return null;
        }

        var bound = sources
            .Where(s => s.ArazzoDocument == step.ArazzoDocument && s.Name == step.Source)
            .Select(s => s.AsyncApiDocument)
            .Distinct()
            .ToList();
        return bound.Count == 1 ? bound[0] : null;
    }

    /// <summary>An <c>AddAsyncApiSource(sourceName, asyncApiDocumentName)</c> call, in the Arazzo document holding it.</summary>
    private sealed class SelfWiredSource(string? arazzoDocument, string name, string? asyncApiDocument)
    {
        public string? ArazzoDocument { get; } = arazzoDocument;

        public string Name { get; } = name;

        public string? AsyncApiDocument { get; } = asyncApiDocument;
    }

    /// <summary>A <c>Channel(sourceName, channelName, action)</c> step, in the Arazzo document holding it.</summary>
    private sealed class ChannelStep(string? arazzoDocument, string source, string channel, Location location)
    {
        public string? ArazzoDocument { get; } = arazzoDocument;

        public string Source { get; } = source;

        public string Channel { get; } = channel;

        public Location Location { get; } = location;
    }
}
