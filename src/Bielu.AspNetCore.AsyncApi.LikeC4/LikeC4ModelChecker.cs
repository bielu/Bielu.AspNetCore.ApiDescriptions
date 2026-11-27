using Bielu.AspNetCore.AsyncApi.LikeC4.Internal;

namespace Bielu.AspNetCore.AsyncApi.LikeC4;

/// <summary>
/// Checks a LikeC4 model, hand-written or not, against the AsyncAPI documents it describes: every relationship it draws
/// between services and channels has operations behind it, and every operation it should show is drawn.
/// </summary>
/// <remarks>
/// <para>
/// The model is read from the JSON <c>likec4 export json</c> writes. An element stands for an AsyncAPI document when
/// its metadata names it (<c>asyncapi 'orders'</c>, the source's name), and for a channel when it names the channel's
/// address (<c>asyncapiChannel 'orders.created'</c>), the way the generated model does. An element inside one of them
/// stands for it too, so a relationship from <c>orders.api</c> counts as one from <c>orders</c>. Elements that stand
/// for neither, such as databases and people, are not checked.
/// </para>
/// <para>
/// A relationship between a service and a channel needs the service to send to or receive from the channel, and one
/// between two services needs one of them to send messages to a channel the other receives the same messages from.
/// Either way round counts, since C4 models draw both data flow and dependency. In the other direction, every
/// operation of a service on a channel the model has needs a relationship between them, and every flow between two
/// services through a channel the model leaves out needs one between the services.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // npx likec4 export json -o likec4.json architecture
/// var findings = LikeC4ModelChecker.Check(
///     File.ReadAllText("likec4.json"),
///     [new AsyncApiLikeC4Source("orders", File.ReadAllText("orders.asyncapi.json"))]);
/// </code>
/// </example>
public static class LikeC4ModelChecker
{
    /// <summary>Checks <paramref name="model"/> against <paramref name="sources"/>.</summary>
    /// <param name="model">The model, as the JSON <c>likec4 export json</c> writes.</param>
    /// <param name="sources">The AsyncAPI documents, named the way the model's <c>asyncapi</c> metadata names them.</param>
    /// <returns>Where they disagree: errors first, in the order found; empty when they agree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Two sources have the same name.</exception>
    /// <exception cref="FormatException">The model is not a LikeC4 JSON export, or a source not an AsyncAPI document.</exception>
    public static IReadOnlyList<LikeC4CheckFinding> Check(string model, IEnumerable<AsyncApiLikeC4Source> sources)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(sources);
        var list = sources.ToList();
        if (list.GroupBy(source => source.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1) is { } duplicate)
        {
            throw new ArgumentException($"Two AsyncAPI documents are named '{duplicate.Key}'.", nameof(sources));
        }

        var export = LikeC4ModelExport.Read(model);
        var documents = list.Select(Read).ToList();
        var operations = documents.SelectMany(document => document.Operations).ToList();
        var addresses = documents.SelectMany(document => document.Addresses).ToHashSet(StringComparer.Ordinal);
        var flows = Flows(operations);
        var findings = new List<LikeC4CheckFinding>();

        // Who each element stands for: its own metadata, else its nearest ancestor's.
        var own = new Dictionary<string, Party>(StringComparer.Ordinal);
        foreach (var element in export.Elements)
        {
            if (Mapping(element, list, addresses, findings) is { } party)
            {
                own[element.Id] = party;
            }
        }

        var titles = export.Elements.ToDictionary(element => element.Id, element => element.Title, StringComparer.Ordinal);
        var drawn = new HashSet<(Party, Party)>();
        var relations = new List<(LikeC4ExportRelation Relation, Party From, Party To)>();
        foreach (var relation in export.Relations)
        {
            if (Resolve(own, relation.Source) is { } from && Resolve(own, relation.Target) is { } to && from != to)
            {
                drawn.Add((from, to));
                drawn.Add((to, from));
                relations.Add((relation, from, to));
            }
        }

        // Relationships the documents do not back.
        foreach (var (relation, from, to) in relations)
        {
            var backed = (from.Kind, to.Kind) switch
            {
                (PartyKind.Service, PartyKind.Channel) => operations.Any(operation => operation.Service == from && operation.Channel == to),
                (PartyKind.Channel, PartyKind.Service) => operations.Any(operation => operation.Service == to && operation.Channel == from),
                (PartyKind.Service, PartyKind.Service) => flows.Any(flow => (flow.From == from && flow.To == to) || (flow.From == to && flow.To == from)),

                // An operation is a service's on a channel: nothing in a document connects two channels.
                _ => false,
            };
            if (!backed)
            {
                var reason = (from.Kind, to.Kind) switch
                {
                    (PartyKind.Service, PartyKind.Service) => "neither sends messages to a channel the other receives them from",
                    (PartyKind.Channel, PartyKind.Channel) => "an operation connects a service to a channel, never two channels",
                    _ => $"{(from.Kind == PartyKind.Service ? from : to)} neither sends to nor receives from {(from.Kind == PartyKind.Channel ? from : to)}",
                };
                findings.Add(new LikeC4CheckFinding(
                    LikeC4CheckSeverity.Error,
                    $"The relationship {Describe(relation, titles)} has no AsyncAPI operation behind it: {reason}.",
                    "relation " + relation.Id));
            }
        }

        var modelled = own.Values.ToHashSet();

        // Operations the model should draw.
        foreach (var operation in operations.DistinctBy(operation => (operation.Service, operation.Channel)))
        {
            if (modelled.Contains(operation.Service) && modelled.Contains(operation.Channel) && !drawn.Contains((operation.Service, operation.Channel)))
            {
                var verb = operation.Action == AsyncApiOperationAction.Send ? "sends to" : "receives from";
                findings.Add(new LikeC4CheckFinding(
                    LikeC4CheckSeverity.Error,
                    $"{Capitalize(operation.Service.ToString())} {verb} {operation.Channel} (operation '{operation.Key}'), but the model has no relationship between them.",
                    Location: null));
            }
        }

        // Only flows through channels the model leaves out need a service-to-service relationship: filter those first,
        // so a modelled channel between two services does not hide another, omitted one between them.
        foreach (var flow in flows.Where(flow => !modelled.Contains(flow.Channel)).DistinctBy(flow => (flow.From, flow.To)))
        {
            if (modelled.Contains(flow.From) && modelled.Contains(flow.To) && !drawn.Contains((flow.From, flow.To)))
            {
                findings.Add(new LikeC4CheckFinding(
                    LikeC4CheckSeverity.Error,
                    $"{Capitalize(flow.From.ToString())} sends {string.Join(", ", flow.Messages)} to {flow.To} through {flow.Channel}, but the model has no relationship between them.",
                    Location: null));
            }
        }

        foreach (var source in list)
        {
            if (!modelled.Contains(Party.Service(source.Name)))
            {
                findings.Add(new LikeC4CheckFinding(
                    LikeC4CheckSeverity.Warning,
                    $"No element stands for AsyncAPI document '{source.Name}': give one the metadata asyncapi '{source.Name}'.",
                    "source " + source.Name));
            }
        }

        return findings.OrderBy(finding => finding.Severity).ToList();
    }

    /// <summary>Who an element's own metadata says it stands for, reporting metadata that names nothing known.</summary>
    private static Party? Mapping(
        LikeC4ExportElement element,
        List<AsyncApiLikeC4Source> sources,
        HashSet<string> addresses,
        List<LikeC4CheckFinding> findings)
    {
        if (element.Metadata.TryGetValue(AsyncApiLikeC4ModelBuilder.ServiceMetadataKey, out var name))
        {
            if (sources.Any(source => string.Equals(source.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return Party.Service(name);
            }

            findings.Add(new LikeC4CheckFinding(
                LikeC4CheckSeverity.Error,
                $"Element '{element.Id}' stands for AsyncAPI document '{name}', which is not one of the documents checked.",
                "element " + element.Id));
            return null;
        }

        if (element.Metadata.TryGetValue(AsyncApiLikeC4ModelBuilder.ChannelMetadataKey, out var address))
        {
            // A channel a document declares is known even when no operation uses it: the generator draws it too.
            if (addresses.Contains(address))
            {
                return Party.ForChannel(address);
            }

            findings.Add(new LikeC4CheckFinding(
                LikeC4CheckSeverity.Error,
                $"Element '{element.Id}' stands for channel '{address}', which no AsyncAPI document declares.",
                "element " + element.Id));
        }

        return null;
    }

    /// <summary>Who an element stands for: its own mapping, else its nearest ancestor's (<c>a.b.c</c>, <c>a.b</c>, <c>a</c>).</summary>
    private static Party? Resolve(Dictionary<string, Party> own, string id)
    {
        for (var current = id; ; current = current[..current.LastIndexOf('.')])
        {
            if (own.TryGetValue(current, out var party))
            {
                return party;
            }

            if (!current.Contains('.'))
            {
                return null;
            }
        }
    }

    /// <summary>The channel addresses a document declares, and its operations on channels with a known address.</summary>
    private static (IReadOnlyCollection<string> Addresses, List<Operation> Operations) Read(AsyncApiLikeC4Source source)
    {
        AsyncApiServiceDescription service;
        try
        {
            service = AsyncApiDocumentReader.Read(source.Document);
        }
        catch (FormatException ex)
        {
            throw new FormatException($"AsyncAPI document '{source.Name}': {ex.Message}", ex);
        }

        var addresses = service.Channels
            .Where(channel => channel.Address is not null)
            .ToDictionary(channel => channel.Key, channel => channel.Address!, StringComparer.Ordinal);
        var operations = new List<Operation>();
        foreach (var operation in service.Operations)
        {
            if (addresses.TryGetValue(operation.ChannelKey, out var address))
            {
                operations.Add(new Operation(Party.Service(source.Name), Party.ForChannel(address), operation.Action, operation.Key, operation.Messages));
            }
        }

        return (addresses.Values, operations);
    }

    /// <summary>Every sender of a channel to every other service receiving the same messages from it.</summary>
    private static List<Flow> Flows(List<Operation> operations)
    {
        var flows = new List<Flow>();
        foreach (var send in operations.Where(operation => operation.Action == AsyncApiOperationAction.Send))
        {
            foreach (var receive in operations.Where(operation =>
                         operation.Action == AsyncApiOperationAction.Receive && operation.Channel == send.Channel && operation.Service != send.Service))
            {
                if (AsyncApiLikeC4ModelBuilder.FlowMessages(send.Messages, receive.Messages) is { } messages)
                {
                    flows.Add(new Flow(send.Service, receive.Service, send.Channel, messages));
                }
            }
        }

        return flows;
    }

    private static string Describe(LikeC4ExportRelation relation, Dictionary<string, string> titles) =>
        $"'{titles.GetValueOrDefault(relation.Source, relation.Source)}' -> '{titles.GetValueOrDefault(relation.Target, relation.Target)}'"
        + (relation.Title is { } title ? $" ({title})" : string.Empty);

    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private enum PartyKind
    {
        Service,
        Channel,
    }

    /// <summary>A service (by its source's name, ignoring case) or a channel (by its address).</summary>
    private sealed record Party(PartyKind Kind, string Name)
    {
        public static Party Service(string name) => new(PartyKind.Service, name.ToLowerInvariant());

        public static Party ForChannel(string address) => new(PartyKind.Channel, address);

        public override string ToString() => Kind == PartyKind.Service ? $"service '{Name}'" : $"channel '{Name}'";
    }

    private sealed record Operation(Party Service, Party Channel, AsyncApiOperationAction Action, string Key, IReadOnlyList<string> Messages);

    private sealed record Flow(Party From, Party To, Party Channel, IReadOnlyList<string> Messages);
}
