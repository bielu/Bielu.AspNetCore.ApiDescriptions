using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.ApiDescriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bielu.AspNetCore.ApiDescriptions;

/// <summary>
/// Registers <see cref="IBuildTimeDocumentSource"/>s for build-time document generation.
/// </summary>
public static class BuildTimeDocumentServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TSource"/> so that <c>dotnet getdocument</c> lists and writes its documents,
    /// together with the single document provider the tool queries. Registering the same source type again has
    /// no further effect.
    /// </summary>
    /// <typeparam name="TSource">The <see cref="IBuildTimeDocumentSource"/> to register as a singleton.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to register services onto.</param>
    /// <returns>The <paramref name="services"/>, for chaining.</returns>
    /// <example>
    /// <code>
    /// public static IServiceCollection AddWorkflows(this IServiceCollection services) =>
    ///     services.AddBuildTimeDocumentSource&lt;WorkflowDocumentSource&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddBuildTimeDocumentSource<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSource>(
        this IServiceCollection services)
        where TSource : class, IBuildTimeDocumentSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBuildTimeDocumentSource, TSource>());
        return services.AddBuildTimeDocumentProvider();
    }

    /// <summary>
    /// Registers the <see cref="IDocumentProvider"/> that <c>dotnet getdocument</c> queries at build time.
    /// </summary>
    /// <remarks>
    /// <c>Microsoft.AspNetCore.OpenApi</c> declares an <c>IDocumentProvider</c> with the same full name, and the
    /// tool asks only the first of the two types it finds. Whichever it finds has to list every document: ours does
    /// so by composing with OpenAPI's provider, and an OpenAPI registration that already exists is replaced by a
    /// proxy forwarding to ours. An <c>AddOpenApi()</c> call made after every call to this method registers OpenAPI's
    /// provider again, and the other documents are then missing if the tool finds OpenAPI's type. The BASYNC010
    /// analyzer warns about that order for <c>AddAsyncApi()</c>.
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The proxy is only resolved by build-time document generation, which runs the app under the JIT.")]
    internal static IServiceCollection AddBuildTimeDocumentProvider(this IServiceCollection services)
    {
        services.TryAddSingleton<BuildTimeDocumentProvider>();
        services.TryAddSingleton<IDocumentProvider>(sp => sp.GetRequiredService<BuildTimeDocumentProvider>());

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
                sp.GetRequiredService<BuildTimeDocumentProvider>()));
        }

        return services;
    }
}
