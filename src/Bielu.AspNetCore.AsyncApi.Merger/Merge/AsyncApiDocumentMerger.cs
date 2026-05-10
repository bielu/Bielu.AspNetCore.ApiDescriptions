// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using ByteBard.AsyncAPI.Readers;

namespace Bielu.AspNetCore.AsyncApi.Merger.Merge;

/// <summary>
/// Service that merges multiple AsyncAPI documents into a single unified document.
/// Combines channels, operations, servers, and components from all source documents.
/// </summary>
public sealed class AsyncApiDocumentMerger
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncApiDocumentMerger"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to fetch remote documents.</param>
    public AsyncApiDocumentMerger(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Merges multiple AsyncAPI documents into a single document.
    /// </summary>
    /// <param name="options">The merge configuration specifying document sources and output settings.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The merged AsyncAPI document.</returns>
    /// <exception cref="ArgumentException">Thrown when no sources are configured.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a document cannot be loaded or parsed.</exception>
    public async Task<AsyncApiDocument> MergeAsync(AsyncApiMergeOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Sources.Count == 0)
        {
            throw new ArgumentException("At least one document source must be configured.", nameof(options));
        }

        var documents = new List<(AsyncApiDocument Document, AsyncApiDocumentSource Source)>();

        foreach (var source in options.Sources)
        {
            var document = await LoadDocumentAsync(source, options.HttpTimeout, cancellationToken).ConfigureAwait(false);
            if(document is null)
            {
                continue;
            }
            documents.Add((document, source));
        }

        return MergeDocuments(documents, options);
    }

    /// <summary>
    /// Merges pre-loaded AsyncAPI documents into a single document.
    /// </summary>
    /// <param name="documents">The documents to merge, each paired with an optional key prefix.</param>
    /// <param name="info">Optional info section for the merged document.</param>
    /// <param name="defaultContentType">Optional default content type.</param>
    /// <returns>The merged AsyncAPI document.</returns>
    public static AsyncApiDocument MergeDocuments(IReadOnlyList<(AsyncApiDocument Document, string? KeyPrefix)> documents, AsyncApiInfo? info = null, string? defaultContentType = null)
    {
        if (documents.Count == 0)
        {
            throw new ArgumentException("At least one document must be provided.", nameof(documents));
        }

        var sources = documents.Select(d => (d.Document, new AsyncApiDocumentSource { Uri = string.Empty, KeyPrefix = d.KeyPrefix })).ToList();
        var options = new AsyncApiMergeOptions
        {
            Info = info,
            DefaultContentType = defaultContentType
        };

        return MergeDocuments(sources, options);
    }

    internal async Task<AsyncApiDocument?> LoadDocumentAsync(AsyncApiDocumentSource source, TimeSpan httpTimeout, CancellationToken cancellationToken)
    {
        var content = await LoadContentAsync(source.Uri, httpTimeout, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return null;
        }
        return ParseDocument(content, source.Uri);
    }

    private async Task<string?> LoadContentAsync(string uri, TimeSpan httpTimeout, CancellationToken cancellationToken)
    {
        try
        {
            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri) &&
                (parsedUri.Scheme == Uri.UriSchemeHttp || parsedUri.Scheme == Uri.UriSchemeHttps))
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(httpTimeout);
                var response = await _httpClient.GetAsync(parsedUri, cts.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            }

            // Treat as file path
            var filePath = uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                ? new Uri(uri).LocalPath
                : uri;

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"AsyncAPI document file not found: '{filePath}'", filePath);
            }

            return await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            return null;
        }
        
    }

    internal static AsyncApiDocument ParseDocument(string content, string sourceUri)
    {
        var reader = new AsyncApiStringReader(new AsyncApiReaderSettings());
        var document = reader.Read(content, out var diagnostic);

        if (diagnostic?.Errors is { Count: > 0 } errs)
        {
            // Surface diagnostic errors so binding/parse issues are not silently swallowed.
            // We log instead of throwing because the merger is best-effort: a partially
            // parseable document may still be useful, and forcing a hard failure would
            // break the gateway when one downstream service emits a non-conformant doc.
            foreach (var error in errs)
            {
                Debug.WriteLine($"[AsyncApiDocumentMerger] Parse error for '{sourceUri}': {error.Message} (pointer: {error.Pointer})");
            }
        }

        return document;
    }

    private static AsyncApiDocument MergeDocuments(IReadOnlyList<(AsyncApiDocument Document, AsyncApiDocumentSource Source)> documents, AsyncApiMergeOptions options)
    {
        var firstDoc = documents[0].Document;

        var merged = new AsyncApiDocument
        {
            Asyncapi = ResolveAsyncApiSpecVersion(documents, options),
            Info = options.Info ?? firstDoc.Info ?? new AsyncApiInfo { Title = "Merged AsyncAPI", Version = "1.0.0" },
            DefaultContentType = options.DefaultContentType ?? firstDoc.DefaultContentType,
            Servers = new Dictionary<string, AsyncApiServer>(),
            Channels = new Dictionary<string, AsyncApiChannel>(),
            Operations = new Dictionary<string, AsyncApiOperation>(),
            Components = new AsyncApiComponents()
        };

        foreach (var (document, source) in documents)
        {
            var prefix = source.KeyPrefix ?? string.Empty;

            // Build a per-section key map (originalKey -> finalMergedKey) for this source
            // BEFORE we mutate any references. The map is used to rewrite all internal
            // $ref pointers in the source document so that when the document is grafted
            // into the merged document the pointers still resolve to the correct items.
            var keyMap = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

            MergeServers(merged, document, prefix, keyMap);
            BuildKeyMap(keyMap, "channels", document.Channels, prefix);
            BuildKeyMap(keyMap, "operations", document.Operations, prefix);
            BuildComponentKeyMaps(keyMap, document.Components, prefix);

            // Rewrite every reference inside the source document using the freshly
            // computed key map. After this point the document carries the merged
            // pointer namespace and can be safely combined with `merged`.
            RewriteReferences(document, keyMap);

            MergeChannels(merged, document, prefix);
            MergeOperations(merged, document, prefix);
            MergeComponents(merged, document, prefix);
        }

        return merged;
    }

    private static void MergeServers(
        AsyncApiDocument merged,
        AsyncApiDocument source,
        string prefix,
        Dictionary<string, Dictionary<string, string>> keyMap)
    {
        if (source.Servers is null)
        {
            return;
        }

        var sectionMap = GetOrCreateSectionMap(keyMap, "servers");

        foreach (var (key, server) in source.Servers)
        {
            // Try to find an already-merged server with identical connection coordinates
            // so that 5 microservices that all talk to the same Kafka broker collapse to
            // a single server entry instead of producing N near-duplicates.
            string? existingKey = null;
            foreach (var (mKey, mServer) in merged.Servers)
            {
                if (AreServersEquivalent(server, mServer))
                {
                    existingKey = mKey;
                    break;
                }
            }

            if (existingKey is not null)
            {
                sectionMap[key] = existingKey;
                continue;
            }

            var mergedKey = GetMergedKey(prefix, key);
            merged.Servers[mergedKey] = server;
            sectionMap[key] = mergedKey;
        }
    }

    private static bool AreServersEquivalent(AsyncApiServer a, AsyncApiServer b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }
        if (a is null || b is null)
        {
            return false;
        }
        return string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Protocol, b.Protocol, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.PathName ?? string.Empty, b.PathName ?? string.Empty, StringComparison.Ordinal)
            && string.Equals(a.ProtocolVersion ?? string.Empty, b.ProtocolVersion ?? string.Empty, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> GetOrCreateSectionMap(
        Dictionary<string, Dictionary<string, string>> keyMap, string section)
    {
        if (!keyMap.TryGetValue(section, out var map))
        {
            map = new Dictionary<string, string>(StringComparer.Ordinal);
            keyMap[section] = map;
        }
        return map;
    }

    private static void BuildKeyMap<TValue>(
        Dictionary<string, Dictionary<string, string>> keyMap,
        string section,
        IDictionary<string, TValue>? source,
        string prefix)
    {
        if (source is null)
        {
            return;
        }
        var map = GetOrCreateSectionMap(keyMap, section);
        foreach (var key in source.Keys)
        {
            map[key] = GetMergedKey(prefix, key);
        }
    }

    private static void BuildComponentKeyMaps(
        Dictionary<string, Dictionary<string, string>> keyMap,
        AsyncApiComponents? components,
        string prefix)
    {
        if (components is null)
        {
            return;
        }
        BuildKeyMap(keyMap, "components/schemas", components.Schemas, prefix);
        BuildKeyMap(keyMap, "components/servers", components.Servers, prefix);
        BuildKeyMap(keyMap, "components/channels", components.Channels, prefix);
        BuildKeyMap(keyMap, "components/operations", components.Operations, prefix);
        BuildKeyMap(keyMap, "components/messages", components.Messages, prefix);
        BuildKeyMap(keyMap, "components/securitySchemes", components.SecuritySchemes, prefix);
        BuildKeyMap(keyMap, "components/parameters", components.Parameters, prefix);
        BuildKeyMap(keyMap, "components/correlationIds", components.CorrelationIds, prefix);
        BuildKeyMap(keyMap, "components/tags", components.Tags, prefix);
        BuildKeyMap(keyMap, "components/operationTraits", components.OperationTraits, prefix);
        BuildKeyMap(keyMap, "components/messageTraits", components.MessageTraits, prefix);
        BuildKeyMap(keyMap, "components/serverBindings", components.ServerBindings, prefix);
        BuildKeyMap(keyMap, "components/channelBindings", components.ChannelBindings, prefix);
        BuildKeyMap(keyMap, "components/operationBindings", components.OperationBindings, prefix);
        BuildKeyMap(keyMap, "components/messageBindings", components.MessageBindings, prefix);
    }

    /// <summary>
    /// Walks the entire document graph and rewrites every <see cref="AsyncApiReference"/>
    /// pointer string from the original key namespace to the merged key namespace.
    /// Without this, a reference like <c>#/servers/kafka</c> coming from a service that
    /// was merged with prefix <c>orderservice_</c> would still point at <c>#/servers/kafka</c>
    /// in the merged document — which no longer exists, producing an invalid AsyncAPI doc.
    /// </summary>
    private static void RewriteReferences(AsyncApiDocument document, Dictionary<string, Dictionary<string, string>> keyMap)
    {
        if (keyMap.Count == 0)
        {
            return;
        }
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Visit(document, keyMap, visited);
    }

    private static void Visit(object? node, Dictionary<string, Dictionary<string, string>> keyMap, HashSet<object> visited)
    {
        if (node is null || node is string || node.GetType().IsPrimitive || !visited.Add(node))
        {
            return;
        }

        if (node is IAsyncApiReferenceable refable && refable.Reference is { Reference: { Length: > 0 } refStr })
        {
            var rewritten = RewriteRefString(refStr, keyMap);
            if (!ReferenceEquals(rewritten, refStr))
            {
                // AsyncApiReference.Reference is a computed property (no setter) that is
                // built from the original string captured by the constructor, so we have
                // to replace the whole reference object rather than mutate it in place.
                refable.Reference = new AsyncApiReference(rewritten, refable.Reference.Type);
            }
            // Reference objects are leaf nodes (the resolved target lives elsewhere); do
            // not descend further as that produces noise and potential cycles.
            return;
        }

        switch (node)
        {
            case System.Collections.IDictionary dict:
                foreach (var v in dict.Values)
                {
                    Visit(v, keyMap, visited);
                }
                return;
            case System.Collections.IEnumerable enumerable:
                foreach (var v in enumerable)
                {
                    Visit(v, keyMap, visited);
                }
                return;
        }

        // Reflect over public readable properties to traverse the object graph. We only
        // care about complex AsyncAPI model types — primitives/strings/enums are skipped.
        var type = node.GetType();
        if (type.Namespace is null || !type.Namespace.StartsWith("ByteBard.AsyncAPI", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0 || !prop.CanRead)
            {
                continue;
            }
            var pt = prop.PropertyType;
            if (pt.IsPrimitive || pt.IsEnum || pt == typeof(string) || pt == typeof(Uri))
            {
                continue;
            }
            object? value;
            try { value = prop.GetValue(node); }
            catch { continue; }
            Visit(value, keyMap, visited);
        }
    }

    private static string RewriteRefString(string refStr, Dictionary<string, Dictionary<string, string>> keyMap)
    {
        // Only rewrite local fragment refs that point at a known section.
        // External refs, refs we don't recognise, and refs already pointing to a
        // merged key are left untouched.
        if (!refStr.StartsWith("#/", StringComparison.Ordinal))
        {
            return refStr;
        }

        var parts = refStr.Substring(2).Split('/');
        // Expected shapes:
        //  servers/{key}
        //  channels/{key}[/...]
        //  operations/{key}[/...]
        //  components/{section}/{key}[/...]
        string section;
        int keyIndex;
        if (parts.Length >= 2 && parts[0] == "components")
        {
            if (parts.Length < 3)
            {
                return refStr;
            }
            section = $"components/{parts[1]}";
            keyIndex = 2;
        }
        else if (parts.Length >= 2)
        {
            section = parts[0];
            keyIndex = 1;
        }
        else
        {
            return refStr;
        }

        if (!keyMap.TryGetValue(section, out var map))
        {
            return refStr;
        }
        var originalKey = parts[keyIndex];
        if (!map.TryGetValue(originalKey, out var newKey) || string.Equals(originalKey, newKey, StringComparison.Ordinal))
        {
            return refStr;
        }
        parts[keyIndex] = newKey;
        return "#/" + string.Join("/", parts);
    }

    private static void MergeChannels(AsyncApiDocument merged, AsyncApiDocument source, string prefix)
    {
        if (source.Channels is null)
        {
            return;
        }

        foreach (var (key, channel) in source.Channels)
        {
            var mergedKey = GetMergedKey(prefix, key);
            merged.Channels.TryAdd(mergedKey, channel);
        }
    }

    private static void MergeOperations(AsyncApiDocument merged, AsyncApiDocument source, string prefix)
    {
        if (source.Operations is null)
        {
            return;
        }

        foreach (var (key, operation) in source.Operations)
        {
            var mergedKey = GetMergedKey(prefix, key);
            merged.Operations.TryAdd(mergedKey, operation);
        }
    }

    private static void MergeComponents(AsyncApiDocument merged, AsyncApiDocument source, string prefix)
    {
        if (source.Components is null)
        {
            return;
        }

        MergeDictionary(merged.Components.Schemas ??= new Dictionary<string, AsyncApiMultiFormatSchema>(), source.Components.Schemas, prefix);
        MergeDictionary(merged.Components.Servers ??= new Dictionary<string, AsyncApiServer>(), source.Components.Servers, prefix);
        MergeDictionary(merged.Components.Channels ??= new Dictionary<string, AsyncApiChannel>(), source.Components.Channels, prefix);
        MergeDictionary(merged.Components.Operations ??= new Dictionary<string, AsyncApiOperation>(), source.Components.Operations, prefix);
        MergeDictionary(merged.Components.Messages ??= new Dictionary<string, AsyncApiMessage>(), source.Components.Messages, prefix);
        MergeDictionary(merged.Components.SecuritySchemes ??= new Dictionary<string, AsyncApiSecurityScheme>(), source.Components.SecuritySchemes, prefix);
        MergeDictionary(merged.Components.Parameters ??= new Dictionary<string, AsyncApiParameter>(), source.Components.Parameters, prefix);
        MergeDictionary(merged.Components.CorrelationIds ??= new Dictionary<string, AsyncApiCorrelationId>(), source.Components.CorrelationIds, prefix);
        MergeDictionary(merged.Components.Tags ??= new Dictionary<string, AsyncApiTag>(), source.Components.Tags, prefix);
        MergeDictionary(merged.Components.OperationTraits ??= new Dictionary<string, AsyncApiOperationTrait>(), source.Components.OperationTraits, prefix);
        MergeDictionary(merged.Components.MessageTraits ??= new Dictionary<string, AsyncApiMessageTrait>(), source.Components.MessageTraits, prefix);
        MergeDictionary(merged.Components.ServerBindings ??= new Dictionary<string, AsyncApiBindings<IServerBinding>>(), source.Components.ServerBindings, prefix);
        MergeDictionary(merged.Components.ChannelBindings ??= new Dictionary<string, AsyncApiBindings<IChannelBinding>>(), source.Components.ChannelBindings, prefix);
        MergeDictionary(merged.Components.OperationBindings ??= new Dictionary<string, AsyncApiBindings<IOperationBinding>>(), source.Components.OperationBindings, prefix);
        MergeDictionary(merged.Components.MessageBindings ??= new Dictionary<string, AsyncApiBindings<IMessageBinding>>(), source.Components.MessageBindings, prefix);
    }

    private static void MergeDictionary<TValue>(IDictionary<string, TValue> target, IDictionary<string, TValue>? source, string prefix)
    {
        if (source is null)
        {
            return;
        }

        foreach (var (key, value) in source)
        {
            var mergedKey = GetMergedKey(prefix, key);
            target.TryAdd(mergedKey, value);
        }
    }

    private static string GetMergedKey(string prefix, string key)
    {
        return string.IsNullOrEmpty(prefix) ? key : $"{prefix}_{key}";
    }

    /// <summary>
    /// Resolves the AsyncAPI spec version for the merged document.
    /// If configured explicitly via options, that value is used.
    /// Otherwise, the highest version found across all source documents is used.
    /// Falls back to "3.0.0" if no version could be determined.
    /// </summary>
    internal static string ResolveAsyncApiSpecVersion(IReadOnlyList<(AsyncApiDocument Document, AsyncApiDocumentSource Source)> documents, AsyncApiMergeOptions options)
    {
        if (!string.IsNullOrEmpty(options.AsyncApiSpecVersion))
        {
            return options.AsyncApiSpecVersion;
        }

        Version? highest = null;
        foreach (var (document, _) in documents)
        {
            if (!string.IsNullOrEmpty(document.Asyncapi) && Version.TryParse(document.Asyncapi, out var parsed))
            {
                if (highest is null || parsed > highest)
                {
                    highest = parsed;
                }
            }
        }

        return highest?.ToString(3) ?? "3.0.0";
    }
}
