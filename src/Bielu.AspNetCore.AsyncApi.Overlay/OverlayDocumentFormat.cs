// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.Overlay;

/// <summary>The wire format a description was serialized into, and must be re-emitted in.</summary>
public enum OverlayDocumentFormat
{
    /// <summary>The description is JSON.</summary>
    Json,

    /// <summary>The description is YAML.</summary>
    Yaml
}
