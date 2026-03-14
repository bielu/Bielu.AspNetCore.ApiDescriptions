// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Diagnostics;

/// <summary>
/// Holds a singleton <see cref="ActivitySource"/> for a specific service.
/// Register one instance per service via DI as a singleton so that
/// the same <see cref="ActivitySource"/> is reused throughout the app lifetime.
/// </summary>
public sealed class ActivitySourceProvider(string sourceName) : IDisposable
{
    /// <summary>
    /// The shared <see cref="ActivitySource"/> for this service.
    /// </summary>
    public ActivitySource ActivitySource { get; } = new(sourceName);

    public void Dispose()
    {
        ActivitySource.Dispose();
    }
}
