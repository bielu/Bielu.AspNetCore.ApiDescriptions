// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;

namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// A no-op implementation of <see cref="IHostLifetime"/> that prevents
/// the host from managing its own lifetime when used for document generation.
/// </summary>
internal sealed class NoopHostLifetime : IHostLifetime
{
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
