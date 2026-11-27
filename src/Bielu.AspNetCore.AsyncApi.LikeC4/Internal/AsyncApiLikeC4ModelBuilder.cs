namespace Bielu.AspNetCore.AsyncApi.LikeC4.Internal;

/// <summary>
/// Builds the LikeC4 model of a set of AsyncAPI documents. Every document is a service. A channel is an element of
/// its own, shared by every document that uses the same address, so a service sending to an address and another
/// receiving from it meet at one channel. A send is a relationship from the service to the channel, a receive one
/// from the channel to the service (the way the message flows), and each sender and receiver of a channel are also
/// joined by a flow, for the view of how services talk to each other.
/// </summary>
internal static class AsyncApiLikeC4ModelBuilder
{
    internal const string ServiceKind = "asyncapiService";
    internal const string ChannelKind = "asyncapiChannel";
    internal const string SendKind = "asyncapiSend";
    internal const string ReceiveKind = "asyncapiReceive";
    internal const string FlowKind = "asyncapiFlow";
    internal const string Tag = "asyncapi";

    /// <summary>The metadata key naming the source of a service element.</summary>
    internal const string ServiceMetadataKey = "asyncapi";

    /// <summary>The metadata key holding the address of a channel element, and of the channel a flow goes through.</summary>
    internal const string ChannelMetadataKey = "asyncapiChannel";

    /// <summary>The metadata key holding the key of the operation a send or receive comes from.</summary>
    internal const string OperationMetadataKey = "asyncapiOperation";

    /// <summary>Adds the documents' elements, relationships and views to <paramref name="build"/>'s model.</summary>
    /// <returns>Where each document's operations and channels ended up, for the workflows that use them.</returns>
    public static AsyncApiModelIndex Build(
        LikeC4Build build,
        IReadOnlyList<(AsyncApiLikeC4Source Source, AsyncApiServiceDescription Service)> documents)
    {
        var index = new AsyncApiModelIndex();
        if (documents.Count == 0)
        {
            return index;
        }

        var model = build.Model;
        model.ElementKinds.Add(new LikeC4ElementKind(ServiceKind, "AsyncAPI service", "rectangle"));
        model.ElementKinds.Add(new LikeC4ElementKind(ChannelKind, "AsyncAPI channel", "queue"));
        model.RelationshipKinds.Add(new LikeC4RelationshipKind(SendKind, "sends to a channel", "solid"));
        model.RelationshipKinds.Add(new LikeC4RelationshipKind(ReceiveKind, "delivers from a channel", "dashed"));
        model.RelationshipKinds.Add(new LikeC4RelationshipKind(FlowKind, "messages between services", "dotted"));
        model.Tags.Add(Tag);

        var ids = build.ElementIds;
        var serviceIds = documents.Select(document => ids.Next(document.Source.Name, "service")).ToList();
        var channels = new Dictionary<string, ChannelElement>(StringComparer.Ordinal);
        var channelOrder = new List<ChannelElement>();
        var operations = new List<(string ServiceId, AsyncApiOperationDescription Operation, ChannelElement Channel)>();
        var ownChannelsOf = new List<Dictionary<string, ChannelElement>>();

        for (var i = 0; i < documents.Count; i++)
        {
            var (source, service) = documents[i];
            model.Elements.Add(ServiceElement(serviceIds[i], source, service));
            var ownChannels = new Dictionary<string, ChannelElement>(StringComparer.Ordinal);
            ownChannelsOf.Add(ownChannels);
            foreach (var channel in service.Channels)
            {
                // A channel with an unknown address is the document's own: nothing says another document means it.
                var identity = channel.Address is { } address ? "address:" + address : $"key:{source.Name}/{channel.Key}";
                if (!channels.TryGetValue(identity, out var element))
                {
                    element = new ChannelElement(channel.Address);
                    channels.Add(identity, element);
                    channelOrder.Add(element);
                }

                element.Add(channel);
                ownChannels[channel.Key] = element;
            }

            foreach (var operation in service.Operations)
            {
                if (ownChannels.TryGetValue(operation.ChannelKey, out var channel))
                {
                    operations.Add((serviceIds[i], operation, channel));
                }
            }
        }

        foreach (var channel in channelOrder)
        {
            channel.Id = ids.Next(channel.Address ?? channel.Key, "channel");
            model.Elements.Add(channel.ToElement());
        }

        foreach (var (serviceId, operation, channel) in operations)
        {
            model.Relationships.Add(OperationRelationship(serviceId, operation, channel));
        }

        model.Relationships.AddRange(Flows(operations));
        AddViews(build, documents, serviceIds);

        for (var i = 0; i < documents.Count; i++)
        {
            var (source, service) = documents[i];
            var channelsByKey = new Dictionary<string, AsyncApiModelChannel>(StringComparer.Ordinal);
            foreach (var channel in service.Channels)
            {
                channelsByKey.TryAdd(channel.Key, new AsyncApiModelChannel(ownChannelsOf[i][channel.Key].Id, channel.Messages));
            }

            var operationsByKey = new Dictionary<string, AsyncApiModelOperation>(StringComparer.Ordinal);
            foreach (var operation in service.Operations)
            {
                if (channelsByKey.TryGetValue(operation.ChannelKey, out var channel))
                {
                    operationsByKey.TryAdd(operation.Key, new AsyncApiModelOperation(operation.Action, channel.Id, operation.Messages));
                }
            }

            index.Add(new AsyncApiModelService(source, serviceIds[i], channelsByKey, operationsByKey));
        }

        return index;
    }

    private static LikeC4Element ServiceElement(string id, AsyncApiLikeC4Source source, AsyncApiServiceDescription service)
    {
        var metadata = new List<KeyValuePair<string, string>> { new(ServiceMetadataKey, source.Name) };
        if (service.Version is not null)
        {
            metadata.Add(new("version", service.Version));
        }

        return new LikeC4Element(id, ServiceKind, service.Title ?? source.Name)
        {
            Description = service.Description,
            Tags = [Tag],
            Links = source.Link is { } link && !link.Any(char.IsWhiteSpace) ? [new LikeC4Link(link, "AsyncAPI document")] : [],
            Metadata = metadata,
        };
    }

    private static LikeC4Relationship OperationRelationship(string serviceId, AsyncApiOperationDescription operation, ChannelElement channel)
    {
        var messages = operation.Messages.Count > 0 ? " " + string.Join(", ", operation.Messages) : string.Empty;
        var send = operation.Action == AsyncApiOperationAction.Send;
        return new LikeC4Relationship(
            send ? serviceId : channel.Id,
            send ? channel.Id : serviceId,
            send ? SendKind : ReceiveKind,
            (send ? "sends" : "delivers") + messages)
        {
            Description = operation.Summary,
            Technology = channel.Technology,
            Tags = [Tag],
            Metadata = [new(OperationMetadataKey, operation.Key)],
        };
    }

    /// <summary>
    /// A flow from every service that sends to a channel to every other service that receives from it, carrying the
    /// messages both name. A sender and receiver that name messages but none in common do not meet. A service that
    /// sends to a channel through several operations, or receives from it through several, meets the other in one
    /// flow carrying every message, in the order the operations name them.
    /// </summary>
    private static IEnumerable<LikeC4Relationship> Flows(
        List<(string ServiceId, AsyncApiOperationDescription Operation, ChannelElement Channel)> operations)
    {
        var flows = new OrderedDictionary<(string Sender, string Receiver, ChannelElement Channel), List<string>>();
        foreach (var (sender, send, channel) in operations.Where(operation => operation.Operation.Action == AsyncApiOperationAction.Send))
        {
            foreach (var (receiver, receive, _) in operations.Where(operation =>
                         operation.Operation.Action == AsyncApiOperationAction.Receive
                         && operation.Channel == channel
                         && operation.ServiceId != sender))
            {
                var messages = send.Messages.Count == 0 ? receive.Messages
                    : receive.Messages.Count == 0 ? send.Messages
                    : send.Messages.Intersect(receive.Messages, StringComparer.Ordinal).ToList();
                if (messages.Count == 0 && send.Messages.Count > 0 && receive.Messages.Count > 0)
                {
                    continue;
                }

                if (!flows.TryGetValue((sender, receiver, channel), out var flow))
                {
                    flows.Add((sender, receiver, channel), flow = []);
                }

                flow.AddRange(messages.Where(message => !flow.Contains(message, StringComparer.Ordinal)));
            }
        }

        foreach (var ((sender, receiver, channel), messages) in flows)
        {
            var metadata = channel.Address is { } address
                ? new List<KeyValuePair<string, string>> { new(ChannelMetadataKey, address) }
                : [];
            yield return new LikeC4Relationship(
                sender,
                receiver,
                FlowKind,
                messages.Count > 0 ? string.Join(", ", messages) : channel.Title)
            {
                Description = $"Through {channel.Title}.",
                Technology = channel.Technology,
                Tags = [Tag],
                Metadata = metadata,
            };
        }
    }

    private static void AddViews(
        LikeC4Build build,
        IReadOnlyList<(AsyncApiLikeC4Source Source, AsyncApiServiceDescription Service)> documents,
        IReadOnlyList<string> serviceIds)
    {
        var model = build.Model;
        var viewIds = build.ViewIds;
        var flows = $"* -> * where kind is {FlowKind}";
        model.Views.Add(new LikeC4View(
            viewIds.Next("asyncapiChannels", "view"),
            "AsyncAPI: services and channels",
            Of: null,
            [$"element.kind = {ServiceKind}", $"element.kind = {ChannelKind}"],
            [flows])
        {
            Description = "Every service, the channels it sends to, and the channels that deliver to it.",
        });
        model.Views.Add(new LikeC4View(
            viewIds.Next("asyncapiServices", "view"),
            "AsyncAPI: service flows",
            Of: null,
            [$"element.kind = {ServiceKind}"],
            [])
        {
            Description = "The services, joined where one sends messages to a channel another receives from.",
        });

        for (var i = 0; i < documents.Count; i++)
        {
            var id = serviceIds[i];
            model.Views.Add(new LikeC4View(
                viewIds.Next("asyncapi " + id, "view"),
                documents[i].Service.Title ?? documents[i].Source.Name,
                id,
                ["*", $"-> {id} ->"],
                [flows])
            {
                Description = "The channels this service sends to and receives from.",
            });
        }
    }

    /// <summary>A channel element, built from every document's channel at its address.</summary>
    private sealed class ChannelElement(string? address)
    {
        private readonly List<string> _protocols = [];
        private string? _title;
        private string? _description;

        public string? Address { get; } = address;

        public string Key { get; private set; } = string.Empty;

        public string Id { get; set; } = string.Empty;

        public string Title => _title ?? (string.IsNullOrWhiteSpace(Address) ? Key : Address);

        public string? Technology => _protocols.Count > 0 ? string.Join(", ", _protocols.Order(StringComparer.Ordinal)) : null;

        /// <summary>Adds what a document says about the channel; the first document to give a title or description wins.</summary>
        public void Add(AsyncApiChannelDescription channel)
        {
            if (Key.Length == 0)
            {
                Key = channel.Key;
            }

            _title ??= channel.Title;
            _description ??= channel.Description;
            foreach (var protocol in channel.Protocols)
            {
                if (!_protocols.Contains(protocol, StringComparer.OrdinalIgnoreCase))
                {
                    _protocols.Add(protocol);
                }
            }
        }

        public LikeC4Element ToElement() => new(Id, ChannelKind, Title)
        {
            Description = _description,
            Technology = Technology,
            Tags = [Tag],
            Metadata = Address is { } address ? [new(ChannelMetadataKey, address)] : [],
        };
    }
}
