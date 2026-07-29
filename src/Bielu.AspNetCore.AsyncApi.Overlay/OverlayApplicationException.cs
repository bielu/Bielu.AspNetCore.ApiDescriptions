// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.Overlay;

/// <summary>
/// Thrown when an overlay cannot be loaded or applied while a description is being served or generated.
/// </summary>
/// <remarks>
/// Failing loudly is deliberate. An overlay that silently does nothing — because its file moved, or its
/// targets no longer match the document — would serve a description that looks right but is missing the
/// transformation a consumer depends on. A thrown exception surfaces as a 500 from the document endpoint
/// and as a failed build from build-time generation, both of which are noticed.
/// </remarks>
public sealed class OverlayApplicationException : Exception
{
    /// <summary>Creates an exception with the given message.</summary>
    public OverlayApplicationException(string message) : base(message)
    {
    }

    /// <summary>Creates an exception with the given message and cause.</summary>
    public OverlayApplicationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
