// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Merger.Merge;

/// <summary>
/// Configuration options for merging multiple AsyncAPI documents.
/// </summary>
public sealed class AsyncApiMergeOptions
{
    /// <summary>
    /// Gets the list of document sources to merge.
    /// </summary>
    public List<AsyncApiDocumentSource> Sources { get; } = [];

    /// <summary>
    /// Gets or sets the info section of the merged document.
    /// If not set, the info from the first source document will be used.
    /// </summary>
    public AsyncApiInfo? Info { get; set; }

    /// <summary>
    /// Gets or sets the AsyncAPI specification version for the merged document (e.g. "3.0.0", "2.6.0").
    /// If not set, the highest version found across all source documents will be used.
    /// </summary>
    public string? AsyncApiSpecVersion { get; set; }

    /// <summary>
    /// Gets or sets the default content type for the merged document.
    /// </summary>
    public string? DefaultContentType { get; set; }

    /// <summary>
    /// Gets or sets the cache duration for remote documents.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the HTTP request timeout for fetching remote documents.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Adds a document source to the merge configuration.
    /// </summary>
    /// <param name="uri">The URI of the document (file path or URL).</param>
    /// <param name="keyPrefix">Optional prefix for keys from this source.</param>
    /// <returns>The options instance for fluent chaining.</returns>
    public AsyncApiMergeOptions AddSource(string uri, string? keyPrefix = null)
    {
        Sources.Add(new AsyncApiDocumentSource { Uri = uri, KeyPrefix = keyPrefix });
        return this;
    }
}
