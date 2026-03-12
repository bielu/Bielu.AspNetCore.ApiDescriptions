// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Merger.Merge;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.AsyncApi.Merger.Extensions;

/// <summary>
/// Extension methods for configuring AsyncAPI document merge services.
/// </summary>
public static class AsyncApiMergeServiceCollectionExtensions
{
    /// <summary>
    /// Adds AsyncAPI document merge services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An action to configure the merge options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncApiMerge(this IServiceCollection services, Action<AsyncApiMergeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AsyncApiMergeOptions();
        configure(options);

        services.AddHttpClient<AsyncApiDocumentMerger>(client =>
        {
            client.Timeout = options.HttpTimeout;
        });

        services.AddSingleton(options);
        services.AddSingleton<CachedAsyncApiMergeService>();

        return services;
    }
}
