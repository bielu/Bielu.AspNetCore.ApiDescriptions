// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Merger.Merge;

/// <summary>
/// A caching wrapper around <see cref="AsyncApiDocumentMerger"/> that caches the merged
/// document and periodically checks remote sources for changes using ETags and Last-Modified headers.
/// </summary>
internal sealed class CachedAsyncApiMergeService : IDisposable
{
    private readonly AsyncApiDocumentMerger _merger;
    private readonly AsyncApiMergeOptions _options;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private AsyncApiDocument? _cachedDocument;
    private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;
    private Dictionary<string, string?> _etags = new();
    private Dictionary<string, DateTimeOffset?> _lastModified = new();

    public CachedAsyncApiMergeService(AsyncApiDocumentMerger merger, AsyncApiMergeOptions options, HttpClient httpClient)
    {
        _merger = merger ?? throw new ArgumentNullException(nameof(merger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Gets the merged document, using a cached version if available and not stale.
    /// </summary>
    public async Task<AsyncApiDocument> GetMergedDocumentAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedDocument is not null && !await HasChangesAsync(cancellationToken).ConfigureAwait(false))
            {
                return _cachedDocument;
            }

            _cachedDocument = await _merger.MergeAsync(_options, cancellationToken).ConfigureAwait(false);
            _lastRefresh = DateTimeOffset.UtcNow;
            await UpdateChangeTrackingAsync(cancellationToken).ConfigureAwait(false);

            return _cachedDocument;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Invalidates the cached document, forcing a refresh on the next request.
    /// </summary>
    public void InvalidateCache()
    {
        _lock.Wait();
        try
        {
            _cachedDocument = null;
            _lastRefresh = DateTimeOffset.MinValue;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<bool> HasChangesAsync(CancellationToken cancellationToken)
    {
        // If cache duration has not elapsed, no need to check
        if (DateTimeOffset.UtcNow - _lastRefresh < _options.CacheDuration)
        {
            return false;
        }

        // Check remote sources for changes
        foreach (var source in _options.Sources)
        {
            if (!IsRemoteUri(source.Uri))
            {
                // For local files, check last write time
                var filePath = source.Uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(source.Uri).LocalPath
                    : source.Uri;

                if (File.Exists(filePath))
                {
                    var lastWrite = File.GetLastWriteTimeUtc(filePath);
                    if (lastWrite > _lastRefresh.UtcDateTime)
                    {
                        return true;
                    }
                }

                continue;
            }

            // For remote sources, use conditional requests (ETag / Last-Modified)
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, source.Uri);

                if (_etags.TryGetValue(source.Uri, out var etag) && etag is not null)
                {
                    request.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag));
                }

                if (_lastModified.TryGetValue(source.Uri, out var lastMod) && lastMod.HasValue)
                {
                    request.Headers.IfModifiedSince = lastMod.Value;
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_options.HttpTimeout);
                using var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

                if (response.StatusCode != System.Net.HttpStatusCode.NotModified)
                {
                    return true;
                }
            }
            catch
            {
                // If we can't check, assume changed to be safe
                return true;
            }
        }

        return false;
    }

    private async Task UpdateChangeTrackingAsync(CancellationToken cancellationToken)
    {
        var newEtags = new Dictionary<string, string?>();
        var newLastModified = new Dictionary<string, DateTimeOffset?>();

        foreach (var source in _options.Sources)
        {
            if (!IsRemoteUri(source.Uri))
            {
                continue;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, source.Uri);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_options.HttpTimeout);
                using var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

                newEtags[source.Uri] = response.Headers.ETag?.Tag;
                newLastModified[source.Uri] = response.Content.Headers.LastModified;
            }
            catch
            {
                // Ignore failures during tracking update
            }
        }

        _etags = newEtags;
        _lastModified = newLastModified;
    }

    private static bool IsRemoteUri(string uri)
    {
        return Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri) &&
               (parsedUri.Scheme == Uri.UriSchemeHttp || parsedUri.Scheme == Uri.UriSchemeHttps);
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
