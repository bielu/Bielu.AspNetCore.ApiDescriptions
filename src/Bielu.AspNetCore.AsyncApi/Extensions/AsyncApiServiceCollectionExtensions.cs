// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Bielu.AspNetCore.AsyncApi.Schemas;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Services.Schemas;
using Bielu.AspNetCore.AsyncApi.Services.XmlDocs;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.ApiDescriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Bielu.AspNetCore.AsyncApi.Extensions;

/// <summary>
/// AsyncApi-related methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class AsyncApiServiceCollectionExtensions
{
    /// <summary>
    /// Adds AsyncApi services related to the given document name to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register services onto.</param>
    /// <param name="documentName">The name of the AsyncApi document associated with registered services.</param>
    /// <example>
    /// This method is commonly used to add AsyncApi services to the <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder.Services"/>
    /// of a <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder"/>, as shown in the following example:
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.Services.AddAsyncApi("MyWebApi");
    /// </code>
    /// </example>
    public static IServiceCollection AddAsyncApi(this IServiceCollection services, string documentName)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddAsyncApi(documentName, _ => { });
    }

    /// <summary>
    /// Adds AsyncApi services related to the given document name to the specified <see cref="IServiceCollection"/> with the specified options.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register services onto.</param>
    /// <param name="documentName">The name of the AsyncApi document associated with registered services.</param>
    /// <param name="configureOptions">A delegate used to configure the target <see cref="AsyncApiOptions"/>.</param>
    /// <example>
    /// This method is commonly used to add AsyncApi services to the <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder.Services"/>
    /// of a <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder"/>, as shown in the following example:
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.Services.AddAsyncApi("MyWebApi", options => {
    ///     // Add a custom schema transformer for decimal types
    ///     options.AddSchemaTransformer(DecimalTransformer.TransformAsync);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAsyncApi(this IServiceCollection services, string documentName, Action<AsyncApiOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // We need to register the document name in a case-insensitive manner to support case-insensitive document name resolution.
        // The document name is used to store and retrieve keyed services and configuration options, which are all case-sensitive.
        // To achieve parity with ASP.NET Core routing, which is case-insensitive, we need to ensure the document name is lowercased.
        var lowercasedDocumentName = documentName.ToLowerInvariant();

        services.AddAsyncApiCore(lowercasedDocumentName);
        services.Configure<AsyncApiOptions>(lowercasedDocumentName, options =>
        {
            options.DocumentName = lowercasedDocumentName;
            configureOptions(options);
        });
        return services;
    }

    /// <summary>
    /// Adds AsyncApi services related to the default document to the specified <see cref="IServiceCollection"/> with the specified options.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register services onto.</param>
    /// <param name="configureOptions">A delegate used to configure the target <see cref="AsyncApiOptions"/>.</param>
    /// <example>
    /// This method is commonly used to add AsyncApi services to the <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder.Services"/>
    /// of a <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder"/>, as shown in the following example:
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.Services.AddAsyncApi(options => {
    ///     // Add a custom schema transformer for decimal types
    ///     options.AddSchemaTransformer(DecimalTransformer.TransformAsync);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAsyncApi(this IServiceCollection services, Action<AsyncApiOptions> configureOptions)
            => services.AddAsyncApi(AsyncApiGeneratorConstants.DefaultDocumentName, configureOptions);

    /// <summary>
    /// Adds AsyncApi services related to the default document to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register services onto.</param>
    /// <example>
    /// This method is commonly used to add AsyncApi services to the <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder.Services"/>
    /// of a <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder"/>, as shown in the following example:
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.Services.AddAsyncApi();
    /// </code>
    /// </example>
    public static IServiceCollection AddAsyncApi(this IServiceCollection services)
        => services.AddAsyncApi(AsyncApiGeneratorConstants.DefaultDocumentName);

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "The reflection-based metadata provider is a default that trimmed and AOT apps replace " +
                        "with the source-generated provider. Schema generation honours the " +
                        "JsonSerializerContext configured on JsonOptions.")]
    private static IServiceCollection AddAsyncApiCore(this IServiceCollection services, string documentName)
    {
        services.AddEndpointsApiExplorer();
        services.TryAddKeyedSingleton<IAsyncApiMetadataProvider, ReflectionAsyncApiMetadataProvider>(documentName);
        services.AddKeyedSingleton<XmlDocumentationProvider>(documentName);
        services.AddKeyedSingleton<AsyncApiJsonSchemaService>(documentName);
        services.AddKeyedSingleton<AsyncApiDocumentService>(documentName);
        services.AddKeyedSingleton<IAsyncApiDocumentProvider, AsyncApiDocumentService>(documentName);

        // Required for build-time generation
        services.AddAsyncApiBuildTimeDocumentProvider();
        // Required to resolve document names for build-time generation
        services.AddSingleton(new NamedService<AsyncApiDocumentService>(documentName));
        // Required to support JSON serializations
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<JsonOptions>, AsyncApiJsonSchemaJsonOptions>());
        return services;
    }

    /// <summary>
    /// Registers the <see cref="IDocumentProvider"/> that <c>dotnet getdocument</c> queries at build time.
    /// </summary>
    /// <remarks>
    /// <c>Microsoft.AspNetCore.OpenApi</c> declares an <c>IDocumentProvider</c> with the same full name, and the
    /// tool asks only the first of the two types it finds. Whichever it finds has to list both libraries'
    /// documents: ours does so by composing with OpenAPI's provider, and an OpenAPI registration that already
    /// exists is replaced by a proxy forwarding to ours. An <c>AddOpenApi()</c> call made after every
    /// <c>AddAsyncApi()</c> call registers OpenAPI's provider again, and the AsyncAPI documents are then missing
    /// if the tool finds OpenAPI's type. The BASYNC010 analyzer warns about that order.
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The proxy is only resolved by build-time document generation, which runs the app under the JIT.")]
    internal static IServiceCollection AddAsyncApiBuildTimeDocumentProvider(this IServiceCollection services)
    {
        services.TryAddSingleton<AsyncApiDocumentProvider>();
        services.TryAddSingleton<IDocumentProvider>(sp => sp.GetRequiredService<AsyncApiDocumentProvider>());

        var openApiDocumentProviderTypes = services
            .Select(descriptor => descriptor.ServiceType)
            .Where(type => type.FullName == OpenApiDocumentProviderAdapter.DocumentProviderInterfaceName
                && type != typeof(IDocumentProvider))
            .Distinct()
            .ToList();
        foreach (var openApiDocumentProviderType in openApiDocumentProviderTypes)
        {
            services.RemoveAll(openApiDocumentProviderType);
            services.AddSingleton(openApiDocumentProviderType, sp => OpenApiDocumentProviderProxy.Create(
                openApiDocumentProviderType,
                sp.GetRequiredService<AsyncApiDocumentProvider>()));
        }

        return services;
    }
}
