using System.Reflection;
using System.Runtime.CompilerServices;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;
using Bielu.AspNetCore.AsyncApi.Helpers;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Services.Schemas;
using Bielu.AspNetCore.AsyncApi.Services.XmlDocs;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI.Bindings.Kafka;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Runtime;
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
            await builder.AddOperationAsync(AsyncApiAction.Receive, endpoint.Uri, endpoint.MessageType!, cancellationToken);
        }
    }

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
        private readonly Dictionary<string, Type> _schemaKeyOwners = new(StringComparer.Ordinal);
        private readonly HashSet<(AsyncApiAction Action, Uri EndpointUri, Type MessageType)> _operations = [];
        private string? _hubPath;

        public async Task AddOperationAsync(AsyncApiAction action, Uri endpointUri, Type messageType, CancellationToken cancellationToken)
        {
            if (!_operations.Add((action, endpointUri, messageType)))
            {
                return;
            }

            var channelKey = GetOrCreateChannel(endpointUri);
            var channel = document.Channels[channelKey];

            var messageKey = await GetOrCreateMessageAsync(messageType, cancellationToken);
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
            document.Operations[operationId] = new AsyncApiOperation
            {
                Action = action,
                Title = operationId,
                Summary = docs?.Summary,
                Channel = new AsyncApiChannelReference($"#/channels/{channelKey}"),
                Messages = [new AsyncApiMessageReference($"#/channels/{channelKey}/messages/{messageKey}")],
                Bindings = OperationBindings(endpointUri, action),
            };
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
                channel.Servers.Add(new AsyncApiServerReference(document.Asyncapi.StartsWith("2.", StringComparison.Ordinal)
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

        private async Task<string> GetOrCreateMessageAsync(Type messageType, CancellationToken cancellationToken)
        {
            var name = messageType.ToMessageTypeName();
            var key = AsyncApiNamingHelper.SanitizeKey(name);

            document.Components ??= new AsyncApiComponents();
            document.Components.Messages ??= new Dictionary<string, AsyncApiMessage>();
            if (document.Components.Messages.ContainsKey(key))
            {
                return key;
            }

            // Registered the way the attribute pipeline registers payloads: keyed by the schema reference id
            // (honoring AsyncApiOptions.CreateSchemaReferenceId), inlined when that opts the type out.
            var (schemaKey, schema) = await schemas.GetOrCreateComponentSchemaAsync(
                document, messageType, services, context.SchemaTransformers, _schemaKeyOwners, cancellationToken);

            var docs = xmlDocs.GetDocumentation(messageType);
            document.Components.Messages[key] = new AsyncApiMessage
            {
                Name = name,
                Title = messageType.Name,
                Summary = docs?.Summary,
                Description = docs?.Remarks,
                Payload = schemaKey is not null
                    ? new AsyncApiJsonSchemaReference($"#/components/schemas/{schemaKey}")
                    : new AsyncApiMultiFormatSchema { Schema = schema as AsyncApiJsonSchema },
            };

            return key;
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
