// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Merger.Merge;

/// <summary>
/// Represents a source for an AsyncAPI document to be included in a merge operation.
/// Sources can be local file paths or remote URLs.
/// </summary>
public sealed class AsyncApiDocumentSource
{
    /// <summary>
    /// Gets or sets the URI of the document source.
    /// Can be a local file path (file:// or absolute path) or a remote URL (http:// or https://).
    /// </summary>
    public required string Uri { get; set; }

    /// <summary>
    /// Gets or sets an optional prefix to add to channel and operation keys from this source
    /// to avoid naming collisions when merging multiple documents.
    /// </summary>
    public string? KeyPrefix { get; set; }
}
