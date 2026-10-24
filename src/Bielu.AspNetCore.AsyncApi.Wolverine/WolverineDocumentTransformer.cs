using System.Reflection;
using System.Runtime.CompilerServices;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.CloudEvents;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;
using Bielu.AspNetCore.AsyncApi.Helpers;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Services.Schemas;
using Bielu.AspNetCore.AsyncApi.Services.XmlDocs;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Bindings.Kafka;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.Runtime;
using Wolverine.Runtime.Handlers;
using Wolverine.Runtime.Routing;
using Wolverine.Util;

namespace Bielu.AspNetCore.AsyncApi.Wolverine;

/// <summary>
/// Adds a channel per external Wolverine endpoint, a message per routed message type and a
/// <c>send</c>/<c>receive</c> operation per route or listener, all read from the running
/// <see cref="IWolverineRuntime"/>.
/// </summary>
internal sealed class WolverineDocumentTransformer(WolverineAsyncApiOptions options) : IAsyncApiDocumentTransformer
{
    private const string SignalRScheme = "signalr";
    private const string KafkaScheme = "kafka";
    private const string WolverineHubTypeName = "Wolverine.SignalR.WolverineHub";

    /// <summary>The <c>datacontenttype</c> Wolverine's <c>CloudEventsMapper</c> writes on every event.</summary>
    private const string CloudEventsDataContentType = "application/json; charset=utf-8";

    /// <summary>The extension attributes Wolverine's <c>CloudEventsMapper</c> writes when the envelope has them.</summary>
    private static readonly Dictionary<string, string> CloudEventsExtensions = new(StringComparer.Ordinal)
    {
        ["tenantid"] = "The Wolverine tenant id of the message, if any.",
        ["topic"] = "The topic or group the message was sent to, if any.",
        ["traceid"] = "The Wolverine correlation id of the message.",
        ["traceparent"] = "The W3C trace context of the activity that sent the message.",
    };

    /// <summary>
    /// Whether <c>InteropWithCloudEvents()</c> was called on the endpoint. Wolverine keeps that only as the
    /// mapping callback it runs when it builds the endpoint's envelope mapper, which a stubbed or not yet started
    /// endpoint never does, so the callback itself is recognized: the compiler names it after the method that
    /// created it. Anything unexpected about the endpoint's shape means "not detected"; list such an endpoint in
    /// <see cref="WolverineAsyncApiOptions.DescribeCloudEvents"/> instead.
    /// </summary>
    internal static bool InteropsWithCloudEvents(Endpoint endpoint)
    {
        for (var type = endpoint.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetField("_customizeMapping", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(endpoint) is Delegate mapping)
            {
                return mapping.GetInvocationList().Any(callback =>
                    callback.Method.Name.Contains("InteropWithCloudEvents", StringComparison.Ordinal));
            }
        }

        return false;
    }

    public async Task TransformAsync(AsyncApiDocument document, AsyncApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var services = context.ApplicationServices;
        var runtime = services.GetService<IWolverineRuntime>() ?? throw new InvalidOperationException(
            $"{nameof(WolverineAsyncApiOptionsExtensions.AddWolverine)}() documents Wolverine's routing, but no Wolverine runtime is " +
            "registered. Add Wolverine to the host (UseWolverine/AddWolverine) or remove AddWolverine() from this AsyncAPI document.");
        var schemas = services.GetRequiredKeyedService<AsyncApiJsonSchemaService>(context.DocumentName);
        var xmlDocs = services.GetRequiredKeyedService<XmlDocumentationProvider>(context.DocumentName);

        var builder = new Builder(document, context, schemas, xmlDocs, options, services);

        var assemblies = options.Assemblies.Count > 0
            ? options.Assemblies
            : runtime.Options.ApplicationAssembly is { } application ? [application] : [];

        // Loaded here rather than through IncludeXmlComments because the default assembly is only
        // known once Wolverine is running; the provider skips files it has already loaded.
        foreach (var assembly in assemblies)
        {
            xmlDocs.Load(AsyncApiOptions.GetXmlDocumentationPath(assembly));
        }

        foreach (var messageType in assemblies.SelectMany(CandidateTypes).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            foreach (var route in runtime.RoutingFor(messageType).Routes.OfType<MessageRoute>())
            {
                if (options.IsIncluded(route.Uri))
                {
                    await builder.AddOperationAsync(AsyncApiAction.Send, route.Uri, messageType, cancellationToken);
                }
            }
        }

        foreach (var endpoint in runtime.Options.Transports.AllEndpoints()
                     .Where(e => e is { IsListener: true, MessageType: not null } && options.IsIncluded(e.Uri))
                     .OrderBy(e => e.Uri.ToString(), StringComparer.Ordinal))
        {
            var operation = await builder.AddOperationAsync(AsyncApiAction.Receive, endpoint.Uri, endpoint.MessageType!, cancellationToken);
            if (operation is not null && !document.IsAsyncApiVersion(AsyncApiVersion.AsyncApi2_0))
            {
                await builder.AddReplyAsync(operation.Value.Id, operation.Value.Operation,
                    ReplyTypes(runtime, endpoint.MessageType!), cancellationToken);
            }
        }

        if (options.CloudEventsDescribed)
        {
            builder.DescribeCloudEvents(runtime);
        }
    }

    /// <summary>
    /// The message types the handlers of <paramref name="messageType"/> return that are documented as its
    /// reply. Wolverine's own types (side effects, <c>OutgoingMessages</c>, ...) and open-ended ones such as
    /// <see cref="object"/> are left out, since they do not describe a payload.
    /// </summary>
    /// <remarks>
    /// A handler does not say whether its return value is a reply: Wolverine sends it to the request's
    /// <c>reply-uri</c> when the request names its type in <c>reply-requested</c>, and publishes it through
    /// the routing rules otherwise. A returned type with no route can therefore only ever leave as a reply,
    /// so it is always documented; a routed one is ambiguous and needs
    /// <see cref="WolverineAsyncApiOptions.IncludeReplies"/>.
    /// </remarks>
    private List<Type> ReplyTypes(IWolverineRuntime runtime, Type messageType) =>
        (runtime as WolverineRuntime)?.Handlers.ChainFor(messageType) is not { } chain
            ? []
            : ReturnedMessageTypes(chain)
                .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                            && t != typeof(object)
                            && t.Assembly != typeof(IWolverineRuntime).Assembly
                            && (options.RepliesIncluded || !runtime.RoutingFor(t).Routes.Any()))
                .Distinct()
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .ToList();

    /// <summary>
    /// What the handlers in <paramref name="chain"/> return as messages. Wolverine publishes each element of a
    /// returned array or collection on its own, but leaves collections out of
    /// <see cref="HandlerChain.PublishedTypes"/>, so their element types are added here.
    /// </summary>
    private static IEnumerable<Type> ReturnedMessageTypes(HandlerChain chain) =>
        chain.PublishedTypes().Concat(chain.Handlers
            .SelectMany(handler => handler.Creates)
            .Select(variable => ElementType(variable.VariableType))
            .OfType<Type>());

    private static Type? ElementType(Type type) =>
        type.IsArray
            ? type.GetElementType()
            : type.GetInterfaces().Append(type)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];

    private static IEnumerable<Type> CandidateTypes(Assembly assembly) =>
        assembly.GetExportedTypes().Where(t =>
            t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
            && !t.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false));

    private sealed class Builder(
        AsyncApiDocument document,
        AsyncApiDocumentTransformerContext context,
        AsyncApiJsonSchemaService schemas,
        XmlDocumentationProvider xmlDocs,
        WolverineAsyncApiOptions options,
        IServiceProvider services)
    {
        private readonly Dictionary<Uri, string> _channelKeys = [];
        private readonly HashSet<(AsyncApiAction Action, Uri EndpointUri, Type MessageType)> _operations = [];
        private readonly Dictionary<string, string> _messageTypeNames = [];
        private string? _hubPath;

        /// <summary>
        /// Describes the messages of the endpoints that send CloudEvents as the structured-mode envelope Wolverine
        /// writes. A message also used on another channel is copied first, so that channel keeps the plain message.
        /// The channel keeps listing the message under the same key, so the operations referencing it are unchanged.
        /// </summary>
        public void DescribeCloudEvents(IWolverineRuntime runtime)
        {
            var cloudEventsChannels = _channelKeys
                .Where(entry => SendsCloudEvents(entry.Key, runtime))
                .Select(entry => entry.Value)
                .ToHashSet(StringComparer.Ordinal);
            if (cloudEventsChannels.Count == 0)
            {
                return;
            }

            var described = new HashSet<string>(StringComparer.Ordinal);
            foreach (var channelKey in cloudEventsChannels.Order(StringComparer.Ordinal))
            {
                var channel = document.Channels[channelKey];
                foreach (var messageKey in channel.Messages.Keys.ToList())
                {
                    if (!_messageTypeNames.TryGetValue(messageKey, out var messageTypeName)
                        || !document.Components.Messages.TryGetValue(messageKey, out var message))
                    {
                        continue;
                    }

                    var sharedWithPlainChannel = document.Channels
                        .Where(other => !cloudEventsChannels.Contains(other.Key))
                        .Any(other => other.Value.Messages.Values.Any(entry => IsReferenceTo(entry, messageKey)));
                    var componentKey = sharedWithPlainChannel ? $"{messageKey}.cloudevent" : messageKey;

                    if (described.Add(componentKey))
                    {
                        if (sharedWithPlainChannel)
                        {
                            message = Copy(message);
                            document.Components.Messages[componentKey] = message;
                        }

                        CloudEventsDocumentTransformer.ApplyStructured(document, message, new CloudEventsDocumentTransformer.EventAttributes(
                            Type: messageTypeName,
                            Source: runtime.Options.ServiceName,
                            DataSchema: null,
                            DataContentType: CloudEventsDataContentType,
                            Extensions: CloudEventsExtensions));
                    }

                    channel.Messages[messageKey] = new AsyncApiMessageReference($"#/components/messages/{componentKey}");
                }
            }
        }

        /// <summary>
        /// Whether the endpoint sends CloudEvents: SignalR always does, a broker endpoint when it was listed in
        /// <see cref="WolverineAsyncApiOptions.DescribeCloudEvents"/> or configured with <c>InteropWithCloudEvents()</c>.
        /// </summary>
        private bool SendsCloudEvents(Uri endpointUri, IWolverineRuntime runtime) =>
            endpointUri.Scheme == SignalRScheme
            || options.CloudEventsEndpoints.Contains(endpointUri)
            || runtime.Options.Transports.AllEndpoints().FirstOrDefault(e => e.Uri == endpointUri) is { } endpoint
            && InteropsWithCloudEvents(endpoint);

        private static bool IsReferenceTo(AsyncApiMessage entry, string messageKey) =>
            entry is AsyncApiMessageReference { Reference.Reference: { } reference }
            && reference == $"#/components/messages/{messageKey}";

        private static AsyncApiMessage Copy(AsyncApiMessage message) => new()
        {
            Name = message.Name,
            Title = message.Title,
            Summary = message.Summary,
            Description = message.Description,
            ContentType = message.ContentType,
            Payload = message.Payload,
            Headers = message.Headers,
            CorrelationId = message.CorrelationId,
            Tags = message.Tags,
            ExternalDocs = message.ExternalDocs,
            Bindings = message.Bindings,
            Traits = message.Traits,
            Examples = message.Examples?.Select(example => new AsyncApiMessageExample
            {
                Name = example.Name,
                Summary = example.Summary,
                Headers = example.Headers is null ? null : new Dictionary<string, AsyncApiAny>(example.Headers),
                Payload = example.Payload?.GetNode() is { } node ? new AsyncApiAny(node.DeepClone()) : null,
            }).ToList(),
        };

        /// <summary>
        /// Adds the operation and returns it with its id, or <see langword="null"/> when the same route was
        /// already documented.
        /// </summary>
        public async Task<(string Id, AsyncApiOperation Operation)?> AddOperationAsync(
            AsyncApiAction action, Uri endpointUri, Type messageType, CancellationToken cancellationToken)
        {
            if (!_operations.Add((action, endpointUri, messageType)))
            {
                return null;
            }

            var channelKey = GetOrCreateChannel(endpointUri);
            var channel = document.Channels[channelKey];

            var messageKey = await GetOrCreateMessageAsync(messageType, endpointUri.Scheme != SignalRScheme, cancellationToken);
            channel.Messages.TryAdd(messageKey, new AsyncApiMessageReference($"#/components/messages/{messageKey}"));

            // The first route of a message gets the short id; any further route is qualified by its channel,
            // and numbered if that still clashes (two operation ids can sanitize to the same key).
            var verb = action == AsyncApiAction.Send ? "send" : "receive";
            var operationId = AsyncApiNamingHelper.SanitizeKey($"{verb}.{messageKey}");
            if (document.Operations.ContainsKey(operationId))
            {
                operationId = UniqueKey(AsyncApiNamingHelper.SanitizeKey($"{verb}.{messageKey}.{channelKey}"), document.Operations.ContainsKey);
            }

            var docs = xmlDocs.GetDocumentation(messageType);
            var operation = new AsyncApiOperation
            {
                Action = action,
                Title = operationId,
                Summary = docs?.Summary,
                Channel = new AsyncApiChannelReference($"#/channels/{channelKey}"),
                Messages = [new AsyncApiMessageReference($"#/channels/{channelKey}/messages/{messageKey}")],
                Bindings = OperationBindings(endpointUri, action),
            };
            ApplyOperationMetadata(operation, messageType);
            document.Operations[operationId] = operation;
            return (operationId, operation);
        }

        /// <summary>
        /// Sets the <c>reply</c> of <paramref name="operation"/> to <paramref name="replyTypes"/>, on a channel
        /// of its own. That channel has no address: Wolverine replies to whichever endpoint the request names
        /// in its <c>reply-uri</c> header, which is what the reply address points at.
        /// </summary>
        public async Task AddReplyAsync(string operationId, AsyncApiOperation operation, IReadOnlyList<Type> replyTypes, CancellationToken cancellationToken)
        {
            if (replyTypes.Count == 0)
            {
                return;
            }

            var channelKey = UniqueKey(AsyncApiNamingHelper.SanitizeKey($"{operationId}.reply"), document.Channels.ContainsKey);
            var channel = new AsyncApiChannel
            {
                Description = $"Replies to '{operationId}'. Wolverine sends them to the endpoint named in the request's '{EnvelopeConstants.ReplyUriKey}' header.",
            };
            document.Channels[channelKey] = channel;

            var reply = new AsyncApiOperationReply
            {
                Address = new AsyncApiOperationReplyAddress
                {
                    Location = $"$message.header#/{EnvelopeConstants.ReplyUriKey}",
                    Description = $"The Wolverine endpoint the reply is sent to. A reply is sent when the request's '{EnvelopeConstants.ReplyRequestedKey}' header names the reply's message type.",
                },
                Channel = new AsyncApiChannelReference($"#/channels/{channelKey}"),
                Messages = [],
            };

            foreach (var replyType in replyTypes)
            {
                var messageKey = await GetOrCreateMessageAsync(replyType, supportsHeaderCorrelation: true, cancellationToken);
                channel.Messages.TryAdd(messageKey, new AsyncApiMessageReference($"#/components/messages/{messageKey}"));
                reply.Messages.Add(new AsyncApiMessageReference($"#/channels/{channelKey}/messages/{messageKey}"));
            }

            operation.Reply = reply;
        }

        /// <summary>
        /// Returns the key of the channel for <paramref name="endpointUri"/>, adding the channel on first use.
        /// Each endpoint gets its own channel, numbered when its address sanitizes to a key already in use
        /// (say a Kafka topic and a RabbitMQ queue both named "orders"), so neither loses its bindings.
        /// </summary>
        private string GetOrCreateChannel(Uri endpointUri)
        {
            if (_channelKeys.TryGetValue(endpointUri, out var existingKey))
            {
                return existingKey;
            }

            var address = ResolveAddress(endpointUri);
            var channelKey = UniqueKey(AsyncApiNamingHelper.SanitizeKey(address), document.Channels.ContainsKey);
            _channelKeys[endpointUri] = channelKey;

            var channel = new AsyncApiChannel
            {
                Address = address,
                Description = endpointUri.Scheme == SignalRScheme
                    ? $"Wolverine's SignalR transport. Clients receive each message on the '{options.SignalRClientMethod}' method as a CloudEvents JSON document whose 'type' is the message name and whose 'data' is the payload."
                    : $"Wolverine endpoint {endpointUri}.",
                Bindings = ChannelBindings(endpointUri, address),
            };

            var serverKey = AsyncApiNamingHelper.SanitizeKey(options.ServerNameFor(endpointUri.Scheme));
            if (document.Servers?.ContainsKey(serverKey) == true)
            {
                channel.Servers.Add(new AsyncApiServerReference(document.IsAsyncApiVersion(AsyncApiVersion.AsyncApi2_0)
                    ? $"#{serverKey}"
                    : $"#/servers/{serverKey}"));
            }

            document.Channels[channelKey] = channel;
            return channelKey;
        }

        private static string UniqueKey(string key, Func<string, bool> isTaken)
        {
            var candidate = key;
            for (var suffix = 2; isTaken(candidate); suffix++)
            {
                candidate = $"{key}.{suffix}";
            }

            return candidate;
        }

        private async Task<string> GetOrCreateMessageAsync(Type messageType, bool supportsHeaderCorrelation, CancellationToken cancellationToken)
        {
            var name = messageType.ToMessageTypeName();
            var key = AsyncApiNamingHelper.SanitizeKey(name);
            _messageTypeNames[key] = name;

            document.Components ??= new AsyncApiComponents();
            document.Components.Messages ??= new Dictionary<string, AsyncApiMessage>();
            if (document.Components.Messages.TryGetValue(key, out var existing))
            {
                // The attribute pipeline (or an earlier route of this same message) may have created this
                // component already; still apply Wolverine's own metadata so it isn't silently dropped.
                ApplyMessageMetadata(existing, messageType, supportsHeaderCorrelation);
                return key;
            }

            // Registered the way the attribute pipeline registers payloads: keyed by the schema reference id
            // (honoring AsyncApiOptions.CreateSchemaReferenceId), inlined when that opts the type out.
            var (schemaKey, schema) = await schemas.GetOrCreateComponentSchemaAsync(
                document, messageType, services, context.SchemaTransformers, cancellationToken);

            // A [Message] on the class supplies the descriptive fields. The component key (and so the
            // channel and operation ids derived from it) stays Wolverine's wire name: Name only changes the
            // displayed `name`, and MessageId, which AsyncAPI documents have no field for here, is ignored.
            var attribute = messageType.GetCustomAttributes<MessageAttribute>(inherit: false)
                .FirstOrDefault(a => a.PayloadType is null || a.PayloadType == messageType);
            var docs = xmlDocs.GetDocumentation(messageType);
            var message = new AsyncApiMessage
            {
                Name = attribute?.Name ?? name,
                Title = attribute?.Title ?? messageType.Name,
                Summary = attribute?.Summary ?? docs?.Summary,
                Description = attribute?.Description ?? docs?.Remarks,
                Payload = schemaKey is not null
                    ? new AsyncApiJsonSchemaReference($"#/components/schemas/{schemaKey}")
                    : new AsyncApiMultiFormatSchema { Schema = schema as AsyncApiJsonSchema },
            };
            document.Components.Messages[key] = message;

            if (attribute is not null)
            {
                AttributeMetadataMapper.ApplyMessageAttribute(document, message, attribute);

                if (attribute.HeadersType is { } headersType)
                {
                    var (headersKey, headersSchema) = await schemas.GetOrCreateComponentSchemaAsync(
                        document, headersType, services, context.SchemaTransformers, cancellationToken);
                    message.Headers = headersKey is not null
                        ? new AsyncApiJsonSchemaReference($"#/components/schemas/{headersKey}")
                        : new AsyncApiMultiFormatSchema { Schema = headersSchema as AsyncApiJsonSchema };
                }
            }

            ApplyMessageMetadata(message, messageType, supportsHeaderCorrelation);
            return key;
        }

        /// <summary>
        /// Applies Wolverine's own message metadata to <paramref name="message"/>. Called once per route of
        /// <paramref name="messageType"/>, so every addition here must be idempotent: it must not duplicate a
        /// reference already present, and it must not erase something an earlier call (or the attribute
        /// pipeline) already set.
        /// </summary>
        private void ApplyMessageMetadata(AsyncApiMessage message, Type messageType, bool supportsHeaderCorrelation)
        {
            // Only ever added, never cleared: a message with routes through both a broker and SignalR still
            // gets the header location, from whichever call here is for the broker route.
            if (options.CorrelationIdIncluded && supportsHeaderCorrelation && message.CorrelationId is null)
            {
                message.CorrelationId = new AsyncApiCorrelationId
                {
                    Location = $"$message.header#/{EnvelopeConstants.CorrelationIdKey}",
                    Description = "The correlation id Wolverine stamps on every envelope it sends through a broker.",
                };
            }

            if (!options.Metadata.TryGetValue(messageType, out var metadata))
            {
                return;
            }

            // Options win over the class attribute, so this replaces whatever [Message] set.
            if (!string.IsNullOrWhiteSpace(metadata.ExternalDocsUrl))
            {
                message.ExternalDocs = new AsyncApiExternalDocumentation
                {
                    Url = new Uri(metadata.ExternalDocsUrl, UriKind.Absolute),
                    Description = metadata.ExternalDocsDescription,
                };
            }

            foreach (var traitKey in metadata.MessageTraits)
            {
                var reference = $"#/components/messageTraits/{AsyncApiNamingHelper.SanitizeKey(traitKey)}";
                message.Traits ??= new List<AsyncApiMessageTrait>();
                var alreadyReferenced = message.Traits
                    .OfType<AsyncApiMessageTraitReference>()
                    .Any(existing => string.Equals(existing.Reference?.Reference, reference, StringComparison.Ordinal));
                if (!alreadyReferenced)
                {
                    message.Traits.Add(new AsyncApiMessageTraitReference(reference));
                }
            }
        }

        private void ApplyOperationMetadata(AsyncApiOperation operation, Type messageType)
        {
            if (!options.Metadata.TryGetValue(messageType, out var metadata))
            {
                return;
            }

            foreach (var traitKey in metadata.OperationTraits)
            {
                operation.Traits ??= new List<AsyncApiOperationTrait>();
                operation.Traits.Add(new AsyncApiOperationTraitReference(
                    $"#/components/operationTraits/{AsyncApiNamingHelper.SanitizeKey(traitKey)}"));
            }

            if (metadata.SecuritySchemes.Count > 0)
            {
                operation.Security ??= new List<AsyncApiSecurityScheme>();
                AuthenticationSchemeDocumentTransformer.AddReferences(
                    operation.Security,
                    metadata.SecuritySchemes.Select(AsyncApiNamingHelper.SanitizeKey).ToList());
            }
        }

        private string ResolveAddress(Uri endpointUri)
        {
            if (options.ChannelAddresses.TryGetValue(endpointUri, out var mapped))
            {
                return mapped;
            }

            if (endpointUri.Scheme == SignalRScheme)
            {
                return _hubPath ??= FindWolverineHubPath(endpointUri);
            }

            var lastSegment = endpointUri.Segments.LastOrDefault(s => s != "/")?.Trim('/');
            return string.IsNullOrEmpty(lastSegment) ? endpointUri.Host : lastSegment;
        }

        private string FindWolverineHubPath(Uri endpointUri)
        {
            var paths = services.GetService<EndpointDataSource>()?.Endpoints
                .OfType<RouteEndpoint>()
                .Where(e => e.Metadata.GetMetadata<HubMetadata>() is { } hub && IsWolverineHub(hub.HubType))
                .Select(e => e.RoutePattern.RawText)
                .OfType<string>()
                .Where(p => !p.EndsWith("/negotiate", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];

            return paths.Count == 1
                ? paths[0]
                : throw new InvalidOperationException(
                    $"Could not work out the channel address for {endpointUri}: found {paths.Count} mapped Wolverine hubs. " +
                    $"Call {nameof(WolverineAsyncApiOptions.MapChannel)}(new Uri(\"{endpointUri}\"), \"/your/hub\") to set it.");
        }

        private static bool IsWolverineHub(Type? type)
        {
            for (; type is not null; type = type.BaseType)
            {
                if (type.FullName == WolverineHubTypeName)
                {
                    return true;
                }
            }

            return false;
        }

        private static AsyncApiBindings<IChannelBinding>? ChannelBindings(Uri endpointUri, string address) => endpointUri.Scheme switch
        {
            SignalRScheme => new AsyncApiBindings<IChannelBinding> { new SignalRChannelBinding { Hub = address } },
            KafkaScheme => new AsyncApiBindings<IChannelBinding> { new KafkaChannelBinding { Topic = address } },
            _ => null,
        };

        private AsyncApiBindings<IOperationBinding>? OperationBindings(Uri endpointUri, AsyncApiAction action) => endpointUri.Scheme switch
        {
            SignalRScheme => new AsyncApiBindings<IOperationBinding>
            {
                new SignalROperationBinding
                {
                    Target = options.SignalRClientMethod,
                    Direction = action == AsyncApiAction.Send
                        ? SignalRProtocol.Directions.ServerToClient
                        : SignalRProtocol.Directions.ClientToServer,
                    CallType = SignalRProtocol.CallTypes.Send,
                },
            },
            _ => null,
        };
    }
}
