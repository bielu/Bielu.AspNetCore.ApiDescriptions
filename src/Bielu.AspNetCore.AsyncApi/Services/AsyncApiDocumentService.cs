// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Services.Schemas;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using AttrOperationType = Bielu.AspNetCore.AsyncApi.Attributes.Attributes.OperationType;

namespace Bielu.AspNetCore.AsyncApi.Services;

internal sealed class AsyncApiDocumentService(
    [ServiceKey]
    string documentName,
    IApiDescriptionGroupCollectionProvider apiDescriptionGroupCollectionProvider,
    IHostEnvironment hostEnvironment,
    IOptionsMonitor<AsyncApiOptions> optionsMonitor,
    IServiceProvider serviceProvider,
    ApplicationPartManager applicationPartManager,
    IServer? server = null) : IAsyncApiDocumentProvider
{
    private readonly AsyncApiOptions _options = optionsMonitor.Get(documentName);

    private readonly AsyncApiJsonSchemaService _componentService =
        serviceProvider.GetRequiredKeyedService<AsyncApiJsonSchemaService>(documentName);

    private readonly ConcurrentDictionary<string, AsyncApiOperationTransformerContext>
        _operationTransformerContextCache = new();

    private static readonly ApiResponseType _defaultApiResponseType = new() { StatusCode = StatusCodes.Status200OK };

    private static readonly FrozenSet<string> _disallowedHeaderParameters =
        new[] { HeaderNames.Accept, HeaderNames.Authorization, HeaderNames.ContentType }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    internal bool TryGetCachedOperationTransformerContext(string descriptionId,
        [NotNullWhen(true)] out AsyncApiOperationTransformerContext? context)
        => _operationTransformerContextCache.TryGetValue(descriptionId, out context);

    public async Task<AsyncApiDocument> GetAsyncApiDocumentAsync(IServiceProvider scopedServiceProvider,
        HttpRequest? httpRequest = null, CancellationToken cancellationToken = default)
    {
        var schemaTransformers = _options.SchemaTransformers.Count > 0
            ? new IAsyncApiSchemaTransformer[_options.SchemaTransformers.Count]
            : [];
        var operationTransformers = _options.OperationTransformers.Count > 0
            ? new IAsyncApiOperationTransformer[_options.OperationTransformers.Count]
            : [];

        InitializeTransformers(scopedServiceProvider, schemaTransformers, operationTransformers);

        var document = new AsyncApiDocument
        {
            Id = $"urn:{SanitizeKey(documentName)}",
            Info = GetAsyncApiInfo(),
            Servers = GetAsyncApiServers(httpRequest),
            Components = new AsyncApiComponents { Schemas = new Dictionary<string, AsyncApiMultiFormatSchema>() },
            Channels = new Dictionary<string, AsyncApiChannel>(StringComparer.Ordinal),
            Operations = new Dictionary<string, AsyncApiOperation>(StringComparer.Ordinal)
        };
        document.Asyncapi = _options.AsyncApiVersion == AsyncApiVersion.AsyncApi2_0 ? "2.6.0" : "3.0.0";
        ApplyBindingsFromOptions(document);

        await PopulateFromAttributeProjectAsync(document, scopedServiceProvider, schemaTransformers, cancellationToken);

        try
        {
            await ApplyTransformersAsync(document, scopedServiceProvider, schemaTransformers, cancellationToken);
        }
        finally
        {
            await FinalizeTransformers(schemaTransformers, operationTransformers);
        }

        if (document.Components?.Schemas is not null)
        {
            document.Components.Schemas = new Dictionary<string, AsyncApiMultiFormatSchema>(
                document.Components.Schemas.OrderBy(kvp => kvp.Key),
                StringComparer.Ordinal);
        }

        // AsyncAPI 2.x requires at least one channel; per requirement, throw if none are defined
        if (_options.AsyncApiVersion == AsyncApiVersion.AsyncApi2_0)
        {
            var hasChannels = document.Channels is not null && document.Channels.Count > 0;
            if (!hasChannels)
            {
                throw new InvalidOperationException("AsyncAPI 2.x requires at least one channel. No channels were discovered for this document.");
            }
        }

        return document;
    }

    public Task<AsyncApiDocument> GetAsyncApiDocumentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return GetAsyncApiDocumentAsync(serviceProvider, httpRequest: null, cancellationToken);
    }

    /// <summary>
    /// Scans candidate assemblies for types marked with AsyncApiAttribute and uses ChannelAttribute, MessageAttribute and OperationAttribute on those types and their members to populate the document's components, channels, messages, and operations.
    /// </summary>
    /// <param name="document">The AsyncApiDocument to populate; its Components, Schemas, and Messages collections will be created or updated.</param>
    /// <param name="schemaTransformers"></param>
    /// <param name="cancellationToken">Token to observe for cancellation of async operations.</param>
    /// <param name="scopedServiceProvider"></param>
    private async Task PopulateFromAttributeProjectAsync(
        AsyncApiDocument document,
        IServiceProvider scopedServiceProvider,
        IAsyncApiSchemaTransformer[] schemaTransformers,
        CancellationToken cancellationToken)
    {
        document.Components ??= new AsyncApiComponents();
        document.Components.Schemas ??= new Dictionary<string, AsyncApiMultiFormatSchema>();
        document.Components.Messages ??= new Dictionary<string, AsyncApiMessage>();

        foreach (var asm in GetCandidateAssembliesForAttributeScan())
        {
            foreach (var type in SafeGetTypes(asm))
            {
                if(type is null) continue;
                var asyncApiAttr = type.GetCustomAttribute<AsyncApiAttribute>(inherit: true);
                if (asyncApiAttr is null)
                    continue;

                if (asyncApiAttr.DocumentName is not null &&
                    !string.Equals(asyncApiAttr.DocumentName, documentName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var members = new List<MemberInfo> { type };
                members.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));

                foreach (var member in members)
                {
                    var channelAttr = member.GetCustomAttribute<ChannelAttribute>(inherit: true);
                    if (channelAttr is null && member is MethodInfo)
                    {
                        channelAttr = type.GetCustomAttribute<ChannelAttribute>(inherit: true);
                    }

                    if (channelAttr is null)
                        continue;

                    var channel = GetOrCreateChannel(document, channelAttr);

                    ApplyChannelParametersFromAttributes(channel, member);
                    ApplyChannelServersFromAttributes(channel, channelAttr);

                    var messageRefs = await ApplyChannelMessagesFromAttributesAsync(
                        document, channel, member, scopedServiceProvider, schemaTransformers, cancellationToken);

                    await ApplyOperationsFromAttributes(document, channel, member, messageRefs, scopedServiceProvider, schemaTransformers, cancellationToken);
                }
            }
        }
    }

    private AsyncApiChannel GetOrCreateChannel(AsyncApiDocument document, ChannelAttribute channelAttr)
    {
        var sanitizedKey = SanitizeKey(channelAttr.Name);
        if (channelAttr.BindingsRef != null && document.Channels.TryGetValue(channelAttr.BindingsRef, out var existingChannelByRef))
        {
            return existingChannelByRef;
        }
        if (document.Channels.TryGetValue(sanitizedKey, out var existing))
        {
            existing.Description ??= channelAttr.Description;
            existing.Address ??= channelAttr.Name;
            return existing;
        }

        var created = new AsyncApiChannel
        {
            Address = channelAttr.Name,
            Description = channelAttr.Description ?? string.Empty,
        };

        document.Channels[sanitizedKey] = created;
        return created;
    }

    private static void ApplyChannelParametersFromAttributes(AsyncApiChannel channel, MemberInfo member)
    {
        var paramAttrs = member.GetCustomAttributes<ChannelParameterAttribute>(inherit: true);
        foreach (var p in paramAttrs)
        {
            if (!channel.Parameters.ContainsKey(p.Name))
            {
                channel.Parameters[p.Name] = new AsyncApiParameter
                {
                    Description = p.Description,
                    Location = p.Location
                };
            }
        }
    }
    private static string SanitizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return key;
        return System.Text.RegularExpressions.Regex.Replace(key, @"[^a-zA-Z0-9\.\-_]", string.Empty);
    }
    private static void ApplyChannelServersFromAttributes(AsyncApiChannel channel, ChannelAttribute channelAttr)
    {
        if (channelAttr.Servers.Length == 0)
            return;

        foreach (var serverKey in channelAttr.Servers.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            var sanitizedServerKey = SanitizeKey(serverKey);
        
            // Avoid duplicates using reflection to check reference ID
            var alreadyExists = channel.Servers.Any(s =>
            {
                if (s is { Reference.Reference: not null } serverRef)
                    return string.Equals(serverRef.Reference.Reference, $"#/servers/{sanitizedServerKey}", StringComparison.OrdinalIgnoreCase);
                return false;
            });

            if (alreadyExists)
                continue;

            // Use proper AsyncAPI 3.0 reference format: #/servers/serverName
            channel.Servers.Add(new AsyncApiServerReference($"#/servers/{sanitizedServerKey}"));
        }
    }

    private async Task<List<string>> ApplyChannelMessagesFromAttributesAsync(
        AsyncApiDocument document,
        AsyncApiChannel channel,
        MemberInfo member,
        IServiceProvider scopedServiceProvider,
        IAsyncApiSchemaTransformer[] schemaTransformers,
        CancellationToken cancellationToken)
    {
        var messageKeys = new List<string>();
        var messageAttrs = member.GetCustomAttributes<MessageAttribute>(inherit: true);

        foreach (var msgAttr in messageAttrs)
        {
            var payloadType = msgAttr.PayloadType;
            var messageKey = SanitizeKey(msgAttr.MessageId
                             ?? msgAttr.Name
                             ?? ToCamelCase(payloadType.Name));

            messageKeys.Add(messageKey);

            if (channel.Messages.ContainsKey(messageKey))
                continue;

            var payloadSchema = await _componentService.GetOrCreateSchemaAsync(
                document,
                payloadType,
                scopedServiceProvider,
                schemaTransformers,
                parameterDescription: null,
                cancellationToken: cancellationToken);

            var schemaKey = SanitizeKey(ToCamelCase(payloadType.Name));
            if (!document.Components.Schemas.ContainsKey(schemaKey))
            {
                document.Components.Schemas[schemaKey] = new AsyncApiMultiFormatSchema
                {
                    Schema = payloadSchema as AsyncApiJsonSchema
                };
            }

            var message = new AsyncApiMessage
            {
                Name = msgAttr.Name ?? messageKey,
                Title = msgAttr.Title ?? messageKey,
                Summary = msgAttr.Summary,
                Description = msgAttr.Description,
                Payload = new AsyncApiJsonSchemaReference($"#/components/schemas/{schemaKey}")
            };

            if (!document.Components.Messages.ContainsKey(messageKey))
            {
                document.Components.Messages[messageKey] = message;
            }

            channel.Messages[messageKey] = new AsyncApiMessageReference($"#/components/messages/{messageKey}");
        }

        return messageKeys;
    }

    /// <summary>
/// Adds AsyncAPI operations to the document for each OperationAttribute found on the given member.
/// </summary>
/// <remarks>
/// Creates operation IDs when missing, ensures payload schemas and message entries exist when a MessagePayloadType is provided (avoiding duplicates), applies tags, sets the operation action and channel reference, and attaches message references to the operation.
/// </remarks>
/// <param name="document">The AsyncAPI document to modify.</param>
/// <param name="channel">The channel to which the operations belong.</param>
/// <param name="member">The reflected member (type or method) that declares OperationAttribute instances.</param>
/// <param name="messageKeys">Existing message keys already associated with the channel; used as the initial set of messages for each operation.</param>
/// <param name="scopedServiceProvider">Scoped service provider used to resolve services during schema creation.</param>
/// <param name="schemaTransformers">Schema transformers applied when creating or retrieving payload schemas.</param>
/// <param name="cancellationToken">Cancellation token to observe while performing async operations.</param>
private async Task ApplyOperationsFromAttributes(
    AsyncApiDocument document,
    AsyncApiChannel channel,
    MemberInfo member,
    List<string> messageKeys,
    IServiceProvider scopedServiceProvider,
    IAsyncApiSchemaTransformer[] schemaTransformers,
    CancellationToken cancellationToken)
{
    var opAttrs = member.GetCustomAttributes<OperationAttribute>(inherit: true);
    foreach (var opAttr in opAttrs)
    {
        var opId = opAttr.OperationId;
        if (string.IsNullOrWhiteSpace(opId))
        {
            opId = SanitizeKey($"{member.DeclaringType?.Name ?? "Type"}_{member.Name}_{opAttr.OperationType}");
        }
        else
        {
            opId = SanitizeKey(opId);
        }

        if (document.Operations.ContainsKey(opId))
            continue;

        // Process MessagePayloadType if present
        var operationMessageKeys = new List<string>(messageKeys);
        if (operationMessageKeys.Count == 0 && opAttr.MessagePayloadType is not null)
        {
            var payloadSchema = await _componentService.GetOrCreateSchemaAsync(
                document,
                opAttr.MessagePayloadType,
                scopedServiceProvider,
                schemaTransformers,
                parameterDescription: null,
                cancellationToken: cancellationToken);

            var schemaKey = SanitizeKey(ToCamelCase(opAttr.MessagePayloadType.Name));
            if (!document.Components.Schemas.ContainsKey(schemaKey))
            {
                document.Components.Schemas[schemaKey] = new AsyncApiMultiFormatSchema
                {
                    Schema = payloadSchema as AsyncApiJsonSchema
                };
            }

            var messageKey = SanitizeKey(ToCamelCase(opAttr.MessagePayloadType.Name));
            if (!document.Components.Messages.ContainsKey(messageKey))
            {
                var message = new AsyncApiMessage
                {
                    Name = messageKey,
                    Title = messageKey,
                    Payload = new AsyncApiJsonSchemaReference($"#/components/schemas/{schemaKey}")
                };
                document.Components.Messages[messageKey] = message;
            }

            if (!channel.Messages.ContainsKey(messageKey))
            {
                channel.Messages[messageKey] = new AsyncApiMessageReference($"#/components/messages/{messageKey}");
            }

            operationMessageKeys.Add(messageKey);
        }

        var op = new AsyncApiOperation
        {
            Title = opAttr.Title ?? opId,
            Summary = opAttr.Summary,
            Description = opAttr.Description,
        };

        op.Action = opAttr.OperationType == AttrOperationType.Subscribe
            ? AsyncApiAction.Send
            : AsyncApiAction.Receive;

        op.Channel = new AsyncApiChannelReference($"#/channels/{SanitizeKey(channel.Address!)}");

        if (opAttr.Tags is { Length: > 0 })
        {
            op.Tags ??= new List<AsyncApiTag>();
            foreach (var tagName in opAttr.Tags)
            {
                op.Tags.Add(new AsyncApiTag { Name = tagName });

                document.Components.Tags ??= new Dictionary<string, AsyncApiTag>();
                if (!document.Components.Tags.ContainsKey(tagName))
                {
                    document.Components.Tags[tagName] = new AsyncApiTag { Name = tagName };
                }
            }
        }

        if (operationMessageKeys.Count > 0)
        {
            op.Messages ??= new List<AsyncApiMessageReference>();
            var channelKey = SanitizeKey(channel.Address!);
            foreach (var msgKey in operationMessageKeys)
            {
                // Reference from operation to channel's message to satisfy subset rule
                var messageRef = new AsyncApiMessageReference($"#/channels/{channelKey}/messages/{msgKey}");
                op.Messages.Add(messageRef);
            }
        }

        document.Operations[opId] = op;
    }
}

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    /// <summary>
    /// Collects assemblies to scan for AsyncApiAttribute usage.
    /// </summary>
    /// <returns>A sequence of distinct assemblies that reference the AsyncApiAttribute assembly, excluding the executing assembly and any dynamic assemblies; includes the entry assembly if present.</returns>
    private IEnumerable<Assembly> GetCandidateAssembliesForAttributeScan()
    {
        var targetAssemblyName = typeof(AsyncApiAttribute).Assembly.GetName();
        var partAssemblies = AppDomain.CurrentDomain.GetAssemblies()  .Where(a => a.FullName != (this).GetType().Assembly.FullName && !a.IsDynamic && 
            a.GetReferencedAssemblies().Any(x=>x.Name == targetAssemblyName.Name));
        var entry = Assembly.GetEntryAssembly();
        return partAssemblies.Concat(entry is not null ? [entry] : []).Distinct();
    }

    /// <summary>
    /// Gets the types declared in the given assembly, excluding any types that failed to load.
    /// </summary>
    /// <param name="asm">The assembly to retrieve types from.</param>
    /// <returns>An enumeration of loaded <see cref="Type"/> objects; types that could not be loaded are omitted.</returns>
    private static IEnumerable<Type?> SafeGetTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null) ?? Enumerable.Empty<Type>(); }
    }

    private static bool TryGet(object target, string propertyName, out object? value)
    {
        var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null || !prop.CanRead)
        {
            value = null;
            return false;
        }
        value = prop.GetValue(target);
        return true;
    }

    internal void InitializeTransformers(
        IServiceProvider scopedServiceProvider,
        IAsyncApiSchemaTransformer[] schemaTransformers,
        IAsyncApiOperationTransformer[] operationTransformers)
    {
        for (var i = 0; i < _options.SchemaTransformers.Count; i++)
        {
            var schemaTransformer = _options.SchemaTransformers[i];
            schemaTransformers[i] = schemaTransformer is TypeBasedAsyncApiSchemaTransformer typeBasedTransformer
                ? typeBasedTransformer.InitializeTransformer(scopedServiceProvider)
                : schemaTransformer;
        }

        for (var i = 0; i < _options.OperationTransformers.Count; i++)
        {
            var operationTransformer = _options.OperationTransformers[i];
            operationTransformers[i] = operationTransformer is TypeBasedAsyncApiOperationTransformer typeBasedTransformer
                ? typeBasedTransformer.InitializeTransformer(scopedServiceProvider)
                : operationTransformer;
        }
    }

    internal static async Task FinalizeTransformers(
        IAsyncApiSchemaTransformer[] schemaTransformers,
        IAsyncApiOperationTransformer[] operationTransformers)
    {
        for (var i = 0; i < schemaTransformers.Length; i++)
            await schemaTransformers[i].FinalizeTransformer();

        for (var i = 0; i < operationTransformers.Length; i++)
            await operationTransformers[i].FinalizeTransformer();
    }

    internal AsyncApiInfo GetAsyncApiInfo()
    {
        var info = new AsyncApiInfo
        {
            Title = $"{hostEnvironment.ApplicationName} | {documentName}",
            Version = AsyncApiGeneratorConstants.DefaultAsyncApiVersion
        };

        // Apply configured info from options if available
        if (_options.Info is not null)
        {
            info.Title = _options.Info.Title ?? info.Title;
            info.Version = _options.Info.Version ?? info.Version;
            info.Description = _options.Info.Description;
            info.License = _options.Info.License;
            info.Contact = _options.Info.Contact;
        }

        return info;
    }
    private void ApplyBindingsFromOptions(AsyncApiDocument document)
    {
        document.Components ??= new AsyncApiComponents();

        // Store bindings in components
        if (_options.OperationBindings.Count > 0)
        {
            document.Components.OperationBindings ??= new Dictionary<string, AsyncApiBindings<IOperationBinding>>();
            foreach (var kvp in _options.OperationBindings)
            {
                if (kvp.Value.Count > 0)
                {
                    var bindings = new AsyncApiBindings<IOperationBinding>();
                    foreach (var binding in kvp.Value)
                    {
                        bindings.Add(binding);
                    }
                    document.Components.OperationBindings[kvp.Key] = bindings;
                }
            }
        }

        if (_options.ChannelBindings.Count > 0)
        {
            document.Components.ChannelBindings ??= new Dictionary<string, AsyncApiBindings<IChannelBinding>>();
            foreach (var kvp in _options.ChannelBindings)
            {
                if (kvp.Value.Count > 0)
                {
                    var bindings = new AsyncApiBindings<IChannelBinding>();
                    foreach (var binding in kvp.Value)
                    {
                        bindings.Add(binding);
                    }

                    var key = SanitizeKey(kvp.Key);
                    document.Components.ChannelBindings[key] = bindings;
                          
                    // Apply bindings to the actual channel if it exists
                    if (document.Channels.TryGetValue(key, out var channel))
                    {
                        channel.Bindings = bindings;
                    }
                }
            }
        }
    }
    internal Dictionary<string, AsyncApiServer> GetAsyncApiServers(HttpRequest? httpRequest = null)
    {
        var servers = new Dictionary<string, AsyncApiServer>();

        // Use configured servers from options if available
        if (_options.Servers.Count > 0)
        {
            foreach (var kvp in _options.Servers)
            {
                servers[SanitizeKey(kvp.Key)] = kvp.Value;
            }
            return servers;
        }

        // Fall back to HTTP request if provided
        if (httpRequest is not null)
        {
            var scheme = httpRequest.Scheme;
            var serverUrl = UriHelper.BuildAbsolute(scheme, httpRequest.Host, httpRequest.PathBase);
            if (serverUrl.EndsWith('/') && !httpRequest.PathBase.HasValue)
                serverUrl = serverUrl.TrimEnd('/');

            servers["default"] = new AsyncApiServer
            {
                Host = serverUrl,
                Protocol = MapProtocolFromScheme(scheme)
            };
            return servers;
        }

        // Last resort: development servers
        return GetDevelopmentAsyncApiServers();
    }

    private static string MapProtocolFromScheme(string scheme)
    {
        return scheme.ToLowerInvariant() switch
        {
            "http" => "http",
            "https" => "https",
            "ws" => "ws",
            "wss" => "wss",
            _ => scheme.ToLowerInvariant()
        };
    }

    private Dictionary<string, AsyncApiServer> GetDevelopmentAsyncApiServers()
    {
        if (hostEnvironment.IsDevelopment() &&
            server?.Features.Get<IServerAddressesFeature>()?.Addresses is { Count: > 0 } addresses)
        {
            var result = new Dictionary<string, AsyncApiServer>();
            var index = 0;
            foreach (var address in addresses)
            {
                var sanitizedAddress = address;
                if (address.Contains("://"))
                {
                    sanitizedAddress = address.Split("://")[1];
                }
                result[$"server{index++}"] = new AsyncApiServer { Host = sanitizedAddress };
            }
            return result;
        }
        return new Dictionary<string, AsyncApiServer>();
    }

    private async Task ApplyTransformersAsync(
        AsyncApiDocument document,
        IServiceProvider scopedServiceProvider,
        IAsyncApiSchemaTransformer[] schemaTransformers,
        CancellationToken cancellationToken)
    {
        var documentTransformerContext = new AsyncApiDocumentTransformerContext
        {
            DocumentName = documentName,
            ApplicationServices = scopedServiceProvider,
            DescriptionGroups = apiDescriptionGroupCollectionProvider.ApiDescriptionGroups.Items,
            Document = document,
            SchemaTransformers = schemaTransformers
        };

        for (var i = 0; i < _options.DocumentTransformers.Count; i++)
        {
            var transformer = _options.DocumentTransformers[i];
            await transformer.TransformAsync(document, documentTransformerContext, cancellationToken);
        }
    }
}
