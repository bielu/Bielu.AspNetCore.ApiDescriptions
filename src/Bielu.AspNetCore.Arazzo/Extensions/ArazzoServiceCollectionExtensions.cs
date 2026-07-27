using Bielu.Arazzo;
using Bielu.AspNetCore.Arazzo.Services;
using Bielu.AspNetCore.Arazzo.SourceResolvers;
using Bielu.AspNetCore.Arazzo.Validation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bielu.AspNetCore.Arazzo.Extensions;

/// <summary>Arazzo-related methods for <see cref="IServiceCollection"/>.</summary>
public static class ArazzoServiceCollectionExtensions
{
    /// <summary>Adds Arazzo services for the named document to the specified <see cref="IServiceCollection"/>.</summary>
    public static IServiceCollection AddArazzo(this IServiceCollection services, string documentName)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddArazzo(documentName, _ => { });
    }

    /// <summary>Adds Arazzo services for the named document to the specified <see cref="IServiceCollection"/> with the specified options.</summary>
    public static IServiceCollection AddArazzo(this IServiceCollection services, string documentName,
        Action<ArazzoOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(documentName);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // See AsyncApiServiceCollectionExtensions.AddAsyncApi for why the document name is lowercased:
        // it doubles as a case-insensitive route value and a case-sensitive keyed-service/options key.
        var lowercasedDocumentName = documentName.ToLowerInvariant();

        services.AddArazzoCore(lowercasedDocumentName);
        services.Configure<ArazzoOptions>(lowercasedDocumentName, options =>
        {
            options.DocumentName = lowercasedDocumentName;
            configureOptions(options);
        });
        return services;
    }

    /// <summary>Adds Arazzo services for the default document to the specified <see cref="IServiceCollection"/> with the specified options.</summary>
    public static IServiceCollection AddArazzo(this IServiceCollection services, Action<ArazzoOptions> configureOptions)
        => services.AddArazzo(ArazzoDefaults.DefaultDocumentName, configureOptions);

    /// <summary>Adds Arazzo services for the default document to the specified <see cref="IServiceCollection"/>.</summary>
    public static IServiceCollection AddArazzo(this IServiceCollection services)
        => services.AddArazzo(ArazzoDefaults.DefaultDocumentName);

    private static void AddArazzoCore(this IServiceCollection services, string documentName)
    {
        services.AddKeyedSingleton<IArazzoDocumentProvider, ArazzoDocumentService>(documentName);
        services.AddKeyedSingleton<ArazzoWorkspaceFactory>(documentName);
        services.AddSingleton(new NamedArazzoDocument(documentName));
        services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, ArazzoStartupValidationStartupFilter>());

        // Registered as IArazzoSourceResolver (not constructed directly by ArazzoWorkspaceFactory) so a
        // consumer can add/replace resolvers through DI, e.g. TryAddEnumerable a custom resolver for
        // "openapi" ahead of/instead of the built-in one — ArazzoWorkspace.RegisterResolver keeps the
        // last one registered per source type.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IArazzoSourceResolver, OpenApiSourceResolver>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IArazzoSourceResolver, AsyncApiSourceResolver>());
    }
}
