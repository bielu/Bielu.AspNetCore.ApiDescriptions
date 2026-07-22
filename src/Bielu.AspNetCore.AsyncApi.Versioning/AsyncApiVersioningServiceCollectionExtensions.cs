// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Bielu.AspNetCore.AsyncApi;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Schemas;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Services.Schemas;
using Bielu.AspNetCore.AsyncApi.Services.XmlDocs;
using Bielu.AspNetCore.AsyncApi.Versioning;
using Microsoft.Extensions.ApiDescriptions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// AsyncApi-related methods for <see cref="IServiceCollection"/> with API versioning support.
/// </summary>
public static class AsyncApiVersioningServiceCollectionExtensions
{
    /// <summary>
    /// Adds AsyncApi services with support for API versioning.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register services onto.</param>
    /// <param name="configure">Optional callback to configure version-specific options.</param>
    /// <returns>The specified <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddAsyncApiForApiVersions(this IServiceCollection services, Action<AsyncApiOptions, ApiVersionDescription>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Add core services with AnyKey fallback
        services.AddAsyncApiVersioningCore();

        // Register the dynamic document names provider
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAsyncApiDocumentNamesProvider, ApiVersionDocumentNamesProvider>());

        // Configure options for each version
        services.AddTransient<IConfigureOptions<AsyncApiOptions>>(sp =>
        {
            var provider = sp.GetRequiredService<IApiVersionDescriptionProvider>();
            return new ConfigureVersioningOptions(provider, configure);
        });

        return services;
    }

    private static IServiceCollection AddAsyncApiVersioningCore(this IServiceCollection services)
    {
        // We use KeyedService.AnyKey to match any version name requested at runtime
        services.AddEndpointsApiExplorer();

        services.TryAddKeyedSingleton<XmlDocumentationProvider>(KeyedService.AnyKey, (sp, key) => ActivatorUtilities.CreateInstance<XmlDocumentationProvider>(sp));
        services.TryAddKeyedSingleton<AsyncApiJsonSchemaService>(KeyedService.AnyKey, (sp, key) =>
        {
            return ActivatorUtilities.CreateInstance<AsyncApiJsonSchemaService>(sp, key?.ToString() ?? "default");
        });
        services.TryAddKeyedSingleton<AsyncApiDocumentService>(KeyedService.AnyKey, (sp, key) =>
        {
            return ActivatorUtilities.CreateInstance<AsyncApiDocumentService>(sp, key?.ToString() ?? "default");
        });
        services.TryAddKeyedSingleton<IAsyncApiDocumentProvider>(KeyedService.AnyKey, (sp, key) => sp.GetRequiredKeyedService<AsyncApiDocumentService>(key));

        // Required for build-time generation
        services.TryAddSingleton<IDocumentProvider, AsyncApiDocumentProvider>();

        // Required to support JSON serializations
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>, AsyncApiJsonSchemaJsonOptions>());

        return services;
    }
}
