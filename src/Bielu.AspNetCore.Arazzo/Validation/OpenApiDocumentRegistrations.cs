using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.Arazzo.Validation;

/// <summary>
/// Answers whether <c>AddOpenApi("name")</c> registered a given document name.
/// </summary>
/// <remarks>
/// A keyed lookup used to answer this question, and it still does on ASP.NET Core 10. ASP.NET Core 11
/// registers <see cref="IOpenApiDocumentProvider"/> for <c>KeyedService.AnyKey</c>, so
/// <c>GetKeyedService&lt;IOpenApiDocumentProvider&gt;("typo")</c> returns a provider for any name and quietly
/// generates a document for it. Both versions record each registered name as a singleton
/// <c>NamedService&lt;OpenApiDocumentService&gt;</c>; Microsoft's own build-time <c>GetDocumentNames()</c>
/// enumerates those, and this does the same. Both types are internal, so they are reached by name. If
/// their shape ever changes, this falls back to the keyed lookup instead of failing startup.
/// </remarks>
internal static class OpenApiDocumentRegistrations
{
    private const string NamedServiceTypeName = "Microsoft.AspNetCore.OpenApi.NamedService`1";
    private const string DocumentServiceTypeName = "Microsoft.AspNetCore.OpenApi.OpenApiDocumentService";

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="documentName"/> was registered with <c>AddOpenApi</c>.
    /// </summary>
    public static bool IsRegistered(IServiceProvider serviceProvider, string documentName)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(documentName);

        var registeredNames = TryGetRegisteredNames(serviceProvider);
        return registeredNames is not null
            ? registeredNames.Contains(documentName, StringComparer.Ordinal)
            : serviceProvider.GetKeyedService<IOpenApiDocumentProvider>(documentName) is not null;
    }

    /// <summary>
    /// The registered document names, or <see langword="null"/> when they cannot be read.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "AddOpenApi constructs NamedService<OpenApiDocumentService> itself, so the types are kept; if they are missing, the caller falls back to the keyed lookup.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "Both generic definitions are instantiated by AddOpenApi and DI; if an instantiation cannot be created, the caller falls back to the keyed lookup.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Name is a public property on a type AddOpenApi instantiates; a missing property yields the fallback.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "IEnumerable<T> over a reference type; if the instantiation cannot be created, the caller falls back to the keyed lookup.")]
    internal static IReadOnlyList<string>? TryGetRegisteredNames(IServiceProvider serviceProvider)
    {
        try
        {
            var assembly = typeof(IOpenApiDocumentProvider).Assembly;
            var namedServiceDefinition = assembly.GetType(NamedServiceTypeName, throwOnError: false);
            var documentServiceType = assembly.GetType(DocumentServiceTypeName, throwOnError: false);
            if (namedServiceDefinition is null || documentServiceType is null)
            {
                return null;
            }

            var namedServiceType = namedServiceDefinition.MakeGenericType(documentServiceType);
            var nameProperty = namedServiceType.GetProperty("Name");
            if (nameProperty?.PropertyType != typeof(string)
                || serviceProvider.GetService(typeof(IEnumerable<>).MakeGenericType(namedServiceType)) is not IEnumerable<object> services)
            {
                return null;
            }

            return services.Select(service => (string?)nameProperty.GetValue(service))
                .OfType<string>()
                .ToList();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
