// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Helpers;
using Bielu.AspNetCore.AsyncApi.Models.Metadata;
using Bielu.AspNetCore.AsyncApi.Services.Schemas;
using Bielu.AspNetCore.AsyncApi.Services.XmlDocs;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
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
    IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions,
    IServer? server = null) : IAsyncApiDocumentProvider
{
    private readonly AsyncApiOptions _options = optionsMonitor.Get(documentName);
    private readonly JsonSerializerOptions _jsonSerializerOptions = jsonOptions.Value.SerializerOptions;

    private readonly AsyncApiJsonSchemaService _componentService =
        serviceProvider.GetRequiredKeyedService<AsyncApiJsonSchemaService>(documentName);

    private readonly XmlDocumentationProvider _xmlDocumentationProvider =
        serviceProvider.GetRequiredKeyedService<XmlDocumentationProvider>(documentName);

    private readonly IAsyncApiMetadataProvider _metadataProvider =
        serviceProvider.GetRequiredKeyedService<IAsyncApiMetadataProvider>(documentName);

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

        foreach (var file in _options.XmlDocumentationFiles)
        {
            _xmlDocumentationProvider.Load(file);
        }

        var document = new AsyncApiDocument
        {
            Id = $"urn:{AsyncApiNamingHelper.SanitizeKey(documentName)}",
            Info = GetAsyncApiInfo(),
            Servers = GetAsyncApiServers(httpRequest),
            Components = new AsyncApiComponents { Schemas = new Dictionary<string, AsyncApiMultiFormatSchema>() },
            Channels = new Dictionary<string, AsyncApiChannel>(StringComparer.Ordinal),
            Operations = new Dictionary<string, AsyncApiOperation>(StringComparer.Ordinal)
        };
        document.Asyncapi = _options.AsyncApiVersion == AsyncApiVersion.AsyncApi2_0 ? "2.6.0" : "3.1.0";
        ApplyBindingsFromOptions(document);

        await PopulateFromAttributeProjectAsync(document, scopedServiceProvider, schemaTransformers, cancellationToken);

        try
        {
            await ApplyTransformersAsync(document, scopedServiceProvider, schemaTransformers, cancellationToken);
        }
        finally
        {
            await FinalizeTransformersAsync(schemaTransformers, operationTransformers);
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

        foreach (var typeMetadata in _metadataProvider.GetMetadata(documentName))
        {
            foreach (var memberMetadata in typeMetadata.Members)
            {
                var channelAttr = memberMetadata.Channel;
                if (channelAttr is null) continue;

                var channelKey = AsyncApiNamingHelper.SanitizeKey(channelAttr.Name);
                if (_options.IncludeOnlyChannels.Count > 0 && !_options.IncludeOnlyChannels.Contains(channelKey))
                    continue;

                var channel = GetOrCreateChannel(document, channelAttr, channelKey, memberMetadata.Member);

                ApplyChannelParametersFromMetadata(channel, memberMetadata);
                ApplyChannelServersFromAttributes(document, channel, channelAttr);

                var messageRefs = await ApplyChannelMessagesFromMetadataAsync(
                    document, channel, memberMetadata, scopedServiceProvider, schemaTransformers, cancellationToken);

                await ApplyOperationsFromMetadataAsync(document, channel, memberMetadata, messageRefs, scopedServiceProvider, schemaTransformers, cancellationToken);
            }
        }
    }

    private AsyncApiChannel GetOrCreateChannel(AsyncApiDocument document, ChannelAttribute channelAttr, string sanitizedKey, MemberInfo member)
    {
        if (document.Channels.TryGetValue(sanitizedKey, out var existing))
        {
            existing.Description ??= channelAttr.Description ?? _xmlDocumentationProvider.GetDocumentation(member)?.Summary;
            existing.Address ??= channelAttr.Name;
            AttachChannelBindings(document, existing, channelAttr.BindingsRef);
            return existing;
        }

        var created = new AsyncApiChannel
        {
            Address = channelAttr.Name,
            Description = channelAttr.Description ?? _xmlDocumentationProvider.GetDocumentation(member)?.Summary ?? string.Empty,
        };

        AttachChannelBindings(document, created, channelAttr.BindingsRef);
        document.Channels[sanitizedKey] = created;
        return created;
    }

    /// <summary>
    /// Attaches a channel bindings item registered in <c>components/channelBindings</c> (via
    /// <see cref="AsyncApiOptions.AddChannelBinding"/>) to the channel referenced by <paramref name="bindingsRef"/>.
    /// </summary>
    private static void AttachChannelBindings(AsyncApiDocument document, AsyncApiChannel channel, string? bindingsRef)
    {
        if (string.IsNullOrWhiteSpace(bindingsRef))
        {
            return;
        }

        if (document.Components?.ChannelBindings is { } registered &&
            registered.TryGetValue(AsyncApiNamingHelper.SanitizeKey(bindingsRef), out var bindings))
        {
            channel.Bindings = bindings;
        }
    }

    /// <summary>
    /// Attaches an operation bindings item registered in <c>components/operationBindings</c> (via
    /// <see cref="AsyncApiOptions.AddOperationBinding"/>) to the operation referenced by <paramref name="bindingsRef"/>.
    /// </summary>
    private static void AttachOperationBindings(AsyncApiDocument document, AsyncApiOperation operation, string? bindingsRef)
    {
        if (string.IsNullOrWhiteSpace(bindingsRef))
        {
            return;
        }

        if (document.Components?.OperationBindings is not { } registered)
        {
            return;
        }

        if (registered.TryGetValue(bindingsRef, out var bindings) ||
            registered.TryGetValue(AsyncApiNamingHelper.SanitizeKey(bindingsRef), out bindings))
        {
            operation.Bindings = bindings;
        }
    }

    private void ApplyChannelParametersFromMetadata(AsyncApiChannel channel, AsyncApiMemberMetadata memberMetadata)
    {
        var paramAttrs = memberMetadata.Parameters;
        var xmlDoc = _xmlDocumentationProvider.GetDocumentation(memberMetadata.Member);
        foreach (var p in paramAttrs)
        {
            if (!channel.Parameters.ContainsKey(p.Name))
            {
                channel.Parameters[p.Name] = new AsyncApiParameter
                {
                    Description = p.Description ?? (xmlDoc?.Parameters?.TryGetValue(p.Name, out var paramDesc) == true ? paramDesc : null),
                    Location = p.Location
                };
            }
        }
    }
    private static void ApplyChannelServersFromAttributes(AsyncApiDocument document, AsyncApiChannel channel, ChannelAttribute channelAttr)
    {
        if (channelAttr.Servers.Length == 0)
            return;

        foreach (var serverKey in channelAttr.Servers.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            var sanitizedServerKey = AsyncApiNamingHelper.SanitizeKey(serverKey);

            // Only add if it exists in root servers to avoid reference errors
            if (document.Servers == null || !document.Servers.ContainsKey(sanitizedServerKey))
            {
                continue;
            }

            // Avoid duplicates by checking the reference string
            var alreadyExists = channel.Servers.Any(s =>
            {
                if (s is { Reference.Reference: not null } serverRef)
                {
                    var reference = serverRef.Reference.Reference;
                    return reference.EndsWith(sanitizedServerKey, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            });

            if (alreadyExists)
                continue;

            // Use correct reference format based on version to avoid ByteBard V2 serializer bug
            // V2 needs FragmentId to be the bare key, which we get with #key
            // V3 needs the full JSON pointer #/servers/key
            if (document.Asyncapi.StartsWith("2."))
            {
                channel.Servers.Add(new AsyncApiServerReference($"#{sanitizedServerKey}"));
            }
            else
            {
                channel.Servers.Add(new AsyncApiServerReference($"#/servers/{sanitizedServerKey}"));
            }
        }
    }

    private async Task<List<string>> ApplyChannelMessagesFromMetadataAsync(
        AsyncApiDocument document,
        AsyncApiChannel channel,
        AsyncApiMemberMetadata memberMetadata,
        IServiceProvider scopedServiceProvider,
        IAsyncApiSchemaTransformer[] schemaTransformers,
        CancellationToken cancellationToken)
    {
        var messageKeys = new List<string>();
        var messageAttrs = memberMetadata.Messages;

        foreach (var msgAttr in messageAttrs)
        {
            var payloadType = msgAttr.PayloadType;
            var messageKey = AsyncApiNamingHelper.SanitizeKey(msgAttr.MessageId
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

            var schemaKey = AsyncApiNamingHelper.SanitizeKey(ToCamelCase(payloadType.Name));
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
                Summary = msgAttr.Summary ?? _xmlDocumentationProvider.GetDocumentation(payloadType)?.Summary,
                Description = msgAttr.Description ?? _xmlDocumentationProvider.GetDocumentation(payloadType)?.Remarks,
                Payload = new AsyncApiJsonSchemaReference($"#/components/schemas/{schemaKey}")
            };

            ApplyMessageExamples(message, payloadSchema as AsyncApiJsonSchema, payloadType, memberMetadata.MessageExamples, scopedServiceProvider);

            if (!document.Components.Messages.ContainsKey(messageKey))
            {
                document.Components.Messages[messageKey] = message;
            }

            channel.Messages[messageKey] = new AsyncApiMessageReference($"#/components/messages/{messageKey}");
        }

        return messageKeys;
    }

    private void ApplyMessageExamples(AsyncApiMessage message, AsyncApiJsonSchema? payloadSchema, Type payloadType, List<MessageExampleAttribute> exampleAttrs, IServiceProvider scopedServiceProvider)
    {
        var examples = new List<AsyncApiMessageExample>();

        // 1. From attributes on the member
        foreach (var attr in exampleAttrs)
        {
            var example = new AsyncApiMessageExample
            {
                Name = attr.Name,
                Summary = attr.Summary
            };

            if (!string.IsNullOrEmpty(attr.Json))
            {
                example.Payload = new AsyncApiAny(JsonNode.Parse(attr.Json));
            }
            else if (attr.ProviderType != null)
            {
                var provider = ActivatorUtilities.CreateInstance(scopedServiceProvider, attr.ProviderType) as IAsyncApiMessageExampleProvider;
                var value = provider?.GetExample();
                if (value != null)
                {
                    example.Payload = new AsyncApiAny(JsonSerializer.SerializeToNode(value, _jsonSerializerOptions));
                }
            }

            examples.Add(example);
        }

        // 2. From fluent options
        if (_options.MessageExamples.TryGetValue(payloadType, out var fluentExamples))
        {
            foreach (var fluentExample in fluentExamples)
            {
                var example = new AsyncApiMessageExample
                {
                    Name = fluentExample.Name,
                    Summary = fluentExample.Summary,
                    Payload = new AsyncApiAny(JsonSerializer.SerializeToNode(fluentExample.Value, _jsonSerializerOptions))
                };

                examples.Add(example);
            }
        }

        if (examples.Count > 0)
        {
            message.Examples = examples;

            if (_options.SetSchemaExampleFromMessageExample && payloadSchema != null)
            {
                payloadSchema.Examples ??= new List<AsyncApiAny>();
                if (payloadSchema.Examples.Count == 0 && examples[0].Payload != null)
                {
                    payloadSchema.Examples.Add(examples[0].Payload);
                }
            }
        }
    }

    /// <summary>
/// Adds AsyncAPI operations to the document for each OperationAttribute found on the given member.
/// </summary>
/// <remarks>
/// Creates operation IDs when missing, ensures payload schemas and message entries exist when a MessagePayloadType is provided (avoiding duplicates), applies tags, sets the operation action and channel reference, and attaches message references to the operation.
/// </remarks>
/// <param name="document">The AsyncAPI document to modify.</param>
/// <param name="channel">The channel to which the operations belong.</param>
/// <param name="memberMetadata">The member metadata (type or method) that declares OperationAttribute instances.</param>
/// <param name="messageKeys">Existing message keys already associated with the channel; used as the initial set of messages for each operation.</param>
/// <param name="scopedServiceProvider">Scoped service provider used to resolve services during schema creation.</param>
/// <param name="schemaTransformers">Schema transformers applied when creating or retrieving payload schemas.</param>
/// <param name="cancellationToken">Cancellation token to observe while performing async operations.</param>
private async Task ApplyOperationsFromMetadataAsync(
    AsyncApiDocument document,
    AsyncApiChannel channel,
    AsyncApiMemberMetadata memberMetadata,
    List<string> messageKeys,
    IServiceProvider scopedServiceProvider,
    IAsyncApiSchemaTransformer[] schemaTransformers,
    CancellationToken cancellationToken)
{
    var opAttrs = memberMetadata.Operations;
    foreach (var opAttr in opAttrs)
    {
        var opId = opAttr.OperationId;
        if (string.IsNullOrWhiteSpace(opId))
        {
            opId = AsyncApiNamingHelper.SanitizeKey($"{memberMetadata.Member.DeclaringType?.Name ?? "Type"}_{memberMetadata.Member.Name}_{opAttr.OperationType}");
        }
        else
        {
            opId = AsyncApiNamingHelper.SanitizeKey(opId);
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

            var schemaKey = AsyncApiNamingHelper.SanitizeKey(ToCamelCase(opAttr.MessagePayloadType.Name));
            if (!document.Components.Schemas.ContainsKey(schemaKey))
            {
                document.Components.Schemas[schemaKey] = new AsyncApiMultiFormatSchema
                {
                    Schema = payloadSchema as AsyncApiJsonSchema
                };
            }

            var messageKey = AsyncApiNamingHelper.SanitizeKey(ToCamelCase(opAttr.MessagePayloadType.Name));
            if (!document.Components.Messages.ContainsKey(messageKey))
            {
                var message = new AsyncApiMessage
                {
                    Name = messageKey,
                    Title = messageKey,
                    Summary = _xmlDocumentationProvider.GetDocumentation(opAttr.MessagePayloadType)?.Summary,
                    Description = _xmlDocumentationProvider.GetDocumentation(opAttr.MessagePayloadType)?.Remarks,
                    Payload = new AsyncApiJsonSchemaReference($"#/components/schemas/{schemaKey}")
                };
                ApplyMessageExamples(message, payloadSchema as AsyncApiJsonSchema, opAttr.MessagePayloadType, memberMetadata.MessageExamples, scopedServiceProvider);
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
            Summary = opAttr.Summary ?? _xmlDocumentationProvider.GetDocumentation(memberMetadata.Member)?.Summary,
            Description = opAttr.Description ?? _xmlDocumentationProvider.GetDocumentation(memberMetadata.Member)?.Remarks,
        };

        op.Action = opAttr.OperationType == AttrOperationType.Subscribe
            ? AsyncApiAction.Send
            : AsyncApiAction.Receive;

        op.Channel = new AsyncApiChannelReference($"#/channels/{AsyncApiNamingHelper.SanitizeKey(channel.Address!)}");

        AttachOperationBindings(document, op, opAttr.BindingsRef);

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
            var channelKey = AsyncApiNamingHelper.SanitizeKey(channel.Address!);
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

    internal static async Task FinalizeTransformersAsync(
        IAsyncApiSchemaTransformer[] schemaTransformers,
        IAsyncApiOperationTransformer[] operationTransformers)
    {
        for (var i = 0; i < schemaTransformers.Length; i++)
            await schemaTransformers[i].FinalizeTransformerAsync();

        for (var i = 0; i < operationTransformers.Length; i++)
            await operationTransformers[i].FinalizeTransformerAsync();
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

                    var key = AsyncApiNamingHelper.SanitizeKey(kvp.Key);
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
                servers[AsyncApiNamingHelper.SanitizeKey(kvp.Key)] = kvp.Value;
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
