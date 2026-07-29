// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Transformers;

/// <summary>
/// The wire format a document was serialized into, so a
/// <see cref="IAsyncApiSerializedDocumentTransformer"/> knows how to parse and re-emit it.
/// </summary>
public enum AsyncApiDocumentFormat
{
    /// <summary>The document was serialized as JSON.</summary>
    Json,

    /// <summary>The document was serialized as YAML.</summary>
    Yaml
}
