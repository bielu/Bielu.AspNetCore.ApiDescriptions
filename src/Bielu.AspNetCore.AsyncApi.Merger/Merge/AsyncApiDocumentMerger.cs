// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

    internal async Task<AsyncApiDocument> LoadDocumentAsync(AsyncApiDocumentSource source, TimeSpan httpTimeout, CancellationToken cancellationToken)
    {
        var content = await LoadContentAsync(source.Uri, httpTimeout, cancellationToken).ConfigureAwait(false);
        return ParseDocument(content, source.Uri);
    }

    private async Task<string> LoadContentAsync(string uri, TimeSpan httpTimeout, CancellationToken cancellationToken)
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

    internal static AsyncApiDocument ParseDocument(string content, string sourceUri)
    {
        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out var diagnostic);

        if (diagnostic.Errors.Count > 0)
        {
            var errors = string.Join("; ", diagnostic.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Failed to parse AsyncAPI document from '{sourceUri}': {errors}");
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

            MergeServers(merged, document, prefix);
            MergeChannels(merged, document, prefix);
            MergeOperations(merged, document, prefix);
            MergeComponents(merged, document, prefix);
        }

        return merged;
    }

    private static void MergeServers(AsyncApiDocument merged, AsyncApiDocument source, string prefix)
    {
        if (source.Servers is null)
        {
            return;
        }

        foreach (var (key, server) in source.Servers)
        {
            var mergedKey = GetMergedKey(prefix, key);
            merged.Servers.TryAdd(mergedKey, server);
        }
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
