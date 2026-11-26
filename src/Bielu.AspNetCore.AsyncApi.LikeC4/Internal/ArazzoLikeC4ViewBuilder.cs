using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bielu.Arazzo.Models;

namespace Bielu.AspNetCore.AsyncApi.LikeC4.Internal;

/// <summary>
/// Adds a dynamic view per Arazzo workflow: its steps, in order, as the calls and messages between whoever makes them
/// and the services they reach. A source description that is one of the AsyncAPI documents (by name, or by its URL
/// being that document's link) is that document's service, and a step on one of its operations or channels sends to
/// or is delivered from the channel's element. Any other source description gets an element of its own.
/// </summary>
/// <remarks>
/// Arazzo has no field for who makes a step's call: its runner makes them all, so every step starts from the client.
/// As in the Scalar Arazzo plugin's sequence view, <c>x-actor</c> on a step or its workflow names someone else (a
/// source description, or anyone), and the document's <c>x-actors</c> registry titles them and names the client
/// (<c>"client": true</c>).
/// </remarks>
internal static partial class ArazzoLikeC4ViewBuilder
{
    internal const string ActorKind = "arazzoActor";
    internal const string SourceKind = "arazzoSource";
    internal const string Tag = "arazzo";
    internal const string ActorExtension = "x-actor";
    internal const string ActorsExtension = "x-actors";
    private const string DefaultClient = "client";

    public static void Build(
        LikeC4Build build,
        AsyncApiModelIndex index,
        IReadOnlyList<(ArazzoLikeC4Source Source, ArazzoDocument Document)> documents)
    {
        if (documents.Count == 0)
        {
            return;
        }

        var model = build.Model;
        model.ElementKinds.Add(new LikeC4ElementKind(ActorKind, "Who runs or takes part in an Arazzo workflow", "person"));
        model.ElementKinds.Add(new LikeC4ElementKind(SourceKind, "An Arazzo source description", "rectangle"));
        model.Tags.Add(Tag);

        // Every workflow's view id is known before any step is drawn, so a step can navigate to a later workflow.
        var views = new Dictionary<(int Document, string Workflow), string>();
        for (var d = 0; d < documents.Count; d++)
        {
            foreach (var workflow in documents[d].Document.Workflows)
            {
                views.TryAdd((d, workflow.WorkflowId), build.ViewIds.Next("arazzo " + workflow.WorkflowId, "workflow"));
            }
        }

        var lanes = new Lanes(build, index);
        for (var d = 0; d < documents.Count; d++)
        {
            var document = documents[d].Document;
            foreach (var workflow in document.Workflows)
            {
                var steps = workflow.Steps
                    .Select(step => Step(lanes, documents, d, workflow, step, views))
                    .OfType<LikeC4DynamicStep>()
                    .ToList();
                model.DynamicViews.Add(new LikeC4DynamicView(
                    views[(d, workflow.WorkflowId)],
                    workflow.Summary ?? workflow.WorkflowId,
                    steps)
                {
                    Description = workflow.Description,
                });
            }
        }
    }

    /// <summary>A step's arrow, or <see langword="null"/> when nothing says what it calls.</summary>
    private static LikeC4DynamicStep? Step(
        Lanes lanes,
        IReadOnlyList<(ArazzoLikeC4Source Source, ArazzoDocument Document)> documents,
        int documentIndex,
        ArazzoWorkflow workflow,
        ArazzoStep step,
        Dictionary<(int Document, string Workflow), string> views)
    {
        var document = documents[documentIndex].Document;
        var actor = lanes.Actor(document, Text(step.Extensions, ActorExtension) ?? Text(workflow.Extensions, ActorExtension));
        var notes = step.Description;

        if (step.WorkflowId is { } workflowId)
        {
            var (target, name) = InvokedWorkflow(documents, documentIndex, workflowId);
            if (target is { } invoked && views.TryGetValue((invoked, name), out var view))
            {
                var invokedWorkflow = documents[invoked].Document.Workflows.First(candidate => candidate.WorkflowId == name);
                return new LikeC4DynamicStep(actor, actor, "runs " + (invokedWorkflow.Summary ?? name)) { Notes = notes, NavigateTo = view };
            }

            var sourceName = SourceReference().Match(workflowId) is { Success: true } reference ? reference.Groups[1].Value : null;
            return lanes.Source(document, sourceName) is { } source
                ? new LikeC4DynamicStep(actor, source.Id, "runs " + name) { Notes = notes }
                : null;
        }

        var (sourceDescription, operationKey, channelKey) = Target(lanes, document, step);
        if (lanes.Source(document, sourceDescription) is not { } lane)
        {
            return null;
        }

        var label = operationKey ?? channelKey ?? step.StepId;
        if (lane.Service is { } service
            && Exchange(service, operationKey, channelKey) is var (channelId, messages, serviceAction))
        {
            // The workflow takes the other side of the service's operation, unless the step says what it does.
            var sends = step.Action is { IsKnown: true } action
                ? action.Value == "send"
                : serviceAction != AsyncApiOperationAction.Send;
            var carried = messages.Count > 0 ? " " + string.Join(", ", messages) : string.Empty;
            return sends
                ? new LikeC4DynamicStep(actor, channelId, "sends" + carried) { Notes = notes }
                : new LikeC4DynamicStep(channelId, actor, "delivers" + carried) { Notes = notes };
        }

        return new LikeC4DynamicStep(actor, lane.Id, label) { Notes = notes };
    }

    /// <summary>
    /// The channel element a step on an AsyncAPI service reaches, the messages it names, and what the service does
    /// there (<see langword="null"/> when the step names a channel rather than an operation).
    /// </summary>
    private static (string ChannelId, IReadOnlyList<string> Messages, AsyncApiOperationAction? Action)? Exchange(
        AsyncApiModelService service, string? operationKey, string? channelKey)
    {
        if (operationKey is not null && service.Operations.TryGetValue(operationKey, out var operation))
        {
            return (operation.ChannelId, operation.Messages, operation.Action);
        }

        if (channelKey is not null && service.Channels.TryGetValue(channelKey, out var channel))
        {
            return (channel.Id, channel.Messages, null);
        }

        return null;
    }

    /// <summary>
    /// The source description a step calls, and the operation or channel key it names: from
    /// <c>$sourceDescriptions.orders.placeOrder</c>, from <c>{$sourceDescriptions.orders.url}#/operations/placeOrder</c>
    /// (or <c>#/channels/…</c>, or an OpenAPI path), or, for a bare <c>operationId</c>, from the AsyncAPI service that
    /// has the operation, else the document's first source description.
    /// </summary>
    private static (string? Source, string? Operation, string? Channel) Target(Lanes lanes, ArazzoDocument document, ArazzoStep step)
    {
        if (step.OperationId is { } operationId)
        {
            if (SourceReference().Match(operationId) is { Success: true } reference)
            {
                return (reference.Groups[1].Value, reference.Groups[2].Value, null);
            }

            var owner = document.SourceDescriptions.FirstOrDefault(source =>
                lanes.Source(document, source.Name)?.Service?.Operations.ContainsKey(operationId) == true);
            return ((owner ?? document.SourceDescriptions.FirstOrDefault())?.Name, operationId, null);
        }

        if ((step.ChannelPath ?? step.OperationPath) is { } path && PathReference().Match(path) is { Success: true } pathReference)
        {
            var pointer = pathReference.Groups[2].Value.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var key = pointer.Length == 2 ? pointer[1].Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal) : null;
            return pointer.FirstOrDefault() switch
            {
                "operations" => (pathReference.Groups[1].Value, key, null),
                "channels" => (pathReference.Groups[1].Value, null, key),
                _ => (pathReference.Groups[1].Value, null, null),
            };
        }

        return (null, null, null);
    }

    /// <summary>
    /// The document and id of the workflow a step invokes: one of its own document's, or, for
    /// <c>$sourceDescriptions.checkout.pay</c>, one of the Arazzo document that source description names.
    /// </summary>
    private static (int? Document, string Workflow) InvokedWorkflow(
        IReadOnlyList<(ArazzoLikeC4Source Source, ArazzoDocument Document)> documents, int documentIndex, string workflowId)
    {
        if (SourceReference().Match(workflowId) is not { Success: true } reference)
        {
            return (documentIndex, workflowId);
        }

        var sourceName = reference.Groups[1].Value;
        var workflow = reference.Groups[2].Value;
        var url = documents[documentIndex].Document.SourceDescriptions.FirstOrDefault(source => source.Name == sourceName)?.Url;
        for (var d = 0; d < documents.Count; d++)
        {
            var (source, document) = documents[d];
            if (string.Equals(source.Name, sourceName, StringComparison.OrdinalIgnoreCase)
                || (url is not null && (string.Equals(document.Self, url, StringComparison.Ordinal) || string.Equals(source.Link, url, StringComparison.Ordinal))))
            {
                return (d, workflow);
            }
        }

        return (null, workflow);
    }

    private static string? Text(IDictionary<string, JsonNode?>? extensions, string name) =>
        extensions is not null && extensions.TryGetValue(name, out var value) ? AsyncApiDocumentReader.Text(value) : null;

    /// <summary><c>$sourceDescriptions.&lt;name&gt;.&lt;rest&gt;</c>.</summary>
    [GeneratedRegex(@"^\$sourceDescriptions\.([^.]+)\.(.+)$")]
    private static partial Regex SourceReference();

    /// <summary><c>{$sourceDescriptions.&lt;name&gt;.url}#&lt;pointer&gt;</c>.</summary>
    [GeneratedRegex(@"^\{\$sourceDescriptions\.([^.}]+)\.url\}#(.*)$")]
    private static partial Regex PathReference();

    /// <summary>A lane of a dynamic view: an element, and the AsyncAPI service behind it, if it is one.</summary>
    private sealed record Lane(string Id, AsyncApiModelService? Service);

    /// <summary>The elements the steps start from and reach, added to the model the first time a step needs one.</summary>
    private sealed class Lanes(LikeC4Build build, AsyncApiModelIndex index)
    {
        private readonly Dictionary<string, string> _actors = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Lane> _sources = new(StringComparer.Ordinal);
        private readonly Dictionary<(ArazzoDocument, string), Lane?> _bySourceName = [];

        /// <summary>The lane of a source description of <paramref name="document"/>, or <see langword="null"/> if it has none of that name.</summary>
        public Lane? Source(ArazzoDocument document, string? name)
        {
            if (name is null)
            {
                return null;
            }

            if (_bySourceName.TryGetValue((document, name), out var known))
            {
                return known;
            }

            var source = document.SourceDescriptions.FirstOrDefault(candidate => candidate.Name == name);
            Lane? lane = null;
            if (source is not null)
            {
                lane = (index.FindByName(source.Name) ?? index.FindByLink(source.Url)) is { } service
                    ? new Lane(service.Id, service)
                    : SourceElement(source);
            }

            _bySourceName[(document, name)] = lane;
            return lane;
        }

        /// <summary>The element of a source description that is not one of the AsyncAPI documents, one per URL.</summary>
        private Lane SourceElement(ArazzoSourceDescription source)
        {
            if (_sources.TryGetValue(source.Url, out var lane))
            {
                return lane;
            }

            var type = source.Type?.Value ?? "openapi";
            var id = build.ElementIds.Next(source.Name, "source");
            build.Model.Elements.Add(new LikeC4Element(id, SourceKind, source.Name)
            {
                Technology = type switch
                {
                    "asyncapi" => "AsyncAPI",
                    "arazzo" => "Arazzo",
                    "openapi" => "OpenAPI",
                    _ => type,
                },
                Tags = [Tag],
                Links = source.Url.Any(char.IsWhiteSpace) ? [] : [new LikeC4Link(source.Url, null)],
                Metadata = [new("arazzoSource", source.Name)],
            });
            lane = new Lane(id, null);
            _sources[source.Url] = lane;
            return lane;
        }

        /// <summary>
        /// The element a step starts from: a source description's when <paramref name="actor"/> names one, the client's
        /// when it names none (or the client), and the actor's own otherwise.
        /// </summary>
        public string Actor(ArazzoDocument document, string? actor)
        {
            var registry = Registry(document);
            var client = registry.FirstOrDefault(entry => entry.Value.Client).Key ?? DefaultClient;
            if (actor is not null && actor != client && Source(document, actor) is { } source)
            {
                return source.Id;
            }

            var name = actor ?? client;
            if (_actors.TryGetValue(name, out var id))
            {
                return id;
            }

            var entry = registry.GetValueOrDefault(name);
            id = build.ElementIds.Next(name, "actor");
            build.Model.Elements.Add(new LikeC4Element(id, ActorKind, entry?.Title ?? (name == DefaultClient ? "Client" : name))
            {
                Description = entry?.Description ?? (name == client ? "Runs the workflows." : null),
                Tags = [Tag],
                Metadata = [new("arazzoActor", name)],
            });
            _actors[name] = id;
            return id;
        }

        /// <summary>The document's <c>x-actors</c> registry: actor name → title, description, and whether it is the client.</summary>
        private static Dictionary<string, Actor> Registry(ArazzoDocument document)
        {
            var registry = new Dictionary<string, Actor>(StringComparer.Ordinal);
            if (document.Extensions is not null
                && document.Extensions.TryGetValue(ActorsExtension, out var value)
                && value is JsonObject actors)
            {
                foreach (var (name, node) in actors)
                {
                    if (node is JsonObject entry)
                    {
                        registry[name] = new Actor(
                            AsyncApiDocumentReader.Text(entry["title"]),
                            AsyncApiDocumentReader.Text(entry["description"]),
                            entry["client"] is JsonValue flag && flag.TryGetValue<bool>(out var isClient) && isClient);
                    }
                }
            }

            return registry;
        }
    }

    private sealed record Actor(string? Title, string? Description, bool Client);
}
