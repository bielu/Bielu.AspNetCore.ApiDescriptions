// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.ApiDescriptions;

/// <summary>
/// Provides an implementation of <see cref="IDocumentProvider"/> to use for build-time generation of AsyncApi documents.
/// </summary>
/// <remarks>
/// When the app also calls <c>AddOpenApi()</c>, this provider serves the OpenAPI documents too, because
/// <c>dotnet getdocument</c> only asks one <c>IDocumentProvider</c> (see <see cref="OpenApiDocumentProviderAdapter"/>).
/// AsyncAPI documents are then listed under their build-time names (<see cref="AsyncApiOptions.BuildTimeDocumentName"/>),
/// so an AsyncAPI <c>v1</c> and an OpenAPI <c>v1</c> are not written to the same file.
/// </remarks>
/// <param name="serviceProvider">The <see cref="IServiceProvider"/> to use.</param>
internal sealed class AsyncApiDocumentProvider(IServiceProvider serviceProvider) : IDocumentProvider
{
    /// <summary>
    /// The prefix given to an AsyncAPI document's build-time name when an OpenAPI document has the same name.
    /// </summary>
    internal const string BuildTimeDocumentNamePrefix = "asyncapi-";

    private readonly Lazy<OpenApiDocumentProviderAdapter?> _openApi =
        new(() => OpenApiDocumentProviderAdapter.TryCreate(serviceProvider));

    /// <summary>
    /// Serializes the document associated with a given build-time document name to
    /// the provided writer.
    /// </summary>
    /// <param name="documentName">The name of the document to resolve.</param>
    /// <param name="writer">A text writer associated with the document to write to.</param>
    public Task GenerateAsync(string documentName, TextWriter writer) =>
        GenerateWithOpenApiVersionAsync(documentName, writer, openApiSpecVersion: null);

    /// <summary>
    /// Serializes the AsyncApi document associated with a given document name to
    /// the provided writer under the provided AsyncApi spec version.
    /// </summary>
    /// <param name="documentName">The name of the document to resolve.</param>
    /// <param name="writer">A text writer associated with the document to write to.</param>
    /// <param name="AsyncApiSpecVersion">The AsyncApi specification version to use when serializing the document.</param>
    public Task GenerateAsync(string documentName, TextWriter writer, AsyncApiVersion AsyncApiSpecVersion)
    {
        var buildTimeNames = GetBuildTimeDocumentNames(out _);
        var asyncApiDocumentName = buildTimeNames.GetValueOrDefault(documentName, documentName);
        return GenerateAsyncApiDocumentAsync(asyncApiDocumentName, writer, AsyncApiSpecVersion);
    }

    /// <summary>
    /// Serializes the document associated with a given build-time document name, routing OpenAPI document
    /// names to <c>Microsoft.AspNetCore.OpenApi</c> with the <c>OpenApiSpecVersion</c> the caller asked for.
    /// </summary>
    /// <param name="documentName">The build-time name of the document to resolve.</param>
    /// <param name="writer">A text writer associated with the document to write to.</param>
    /// <param name="openApiSpecVersion">The <c>Microsoft.OpenApi.OpenApiSpecVersion</c> to use for an OpenAPI document, if any.</param>
    internal Task GenerateWithOpenApiVersionAsync(string documentName, TextWriter writer, object? openApiSpecVersion)
    {
        var buildTimeNames = GetBuildTimeDocumentNames(out var openApiNames);
        if (buildTimeNames.TryGetValue(documentName, out var asyncApiDocumentName))
        {
            return GenerateAsyncApiDocumentAsync(asyncApiDocumentName, writer);
        }

        if (openApiNames.Contains(documentName))
        {
            return _openApi.Value!.GenerateAsync(documentName, writer, openApiSpecVersion);
        }

        // Not a listed name: keep resolving it as an AsyncAPI document name, which is what versioned
        // documents registered against KeyedService.AnyKey rely on.
        return GenerateAsyncApiDocumentAsync(documentName, writer);
    }

    /// <summary>
    /// Provides all document names that are currently managed in the application: the OpenAPI document
    /// names, if any, and the build-time names of the AsyncApi documents.
    /// </summary>
    public IEnumerable<string> GetDocumentNames()
    {
        var buildTimeNames = GetBuildTimeDocumentNames(out var openApiNames);
        return openApiNames.Concat(buildTimeNames.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private Task GenerateAsyncApiDocumentAsync(string documentName, TextWriter writer)
    {
        // See AsyncApiServiceCollectionExtensions.cs to learn why we lowercase the document name
        var lowercasedDocumentName = documentName.ToLowerInvariant();

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>();
        var namedOption = options.Get(lowercasedDocumentName);
        var resolvedAsyncApiVersion = namedOption.AsyncApiVersion;
        return GenerateAsyncApiDocumentAsync(lowercasedDocumentName, writer, resolvedAsyncApiVersion);
    }

    private async Task GenerateAsyncApiDocumentAsync(string documentName, TextWriter writer, AsyncApiVersion AsyncApiSpecVersion)
    {
        // We need to retrieve the document name in a case-insensitive manner to support case-insensitive document name resolution.
        // The document service is registered with a key equal to the document name, but in lowercase.
        // The GetRequiredKeyedService() method is case-sensitive, which doesn't work well for AsyncApi document names here,
        // as the document name is also used as the route to retrieve the document, so we need to ensure this is lowercased to achieve consistency with ASP.NET Core routing.
        // See AsyncApiServiceCollectionExtensions.cs for more info.
        var lowercasedDocumentName = documentName.ToLowerInvariant();

        var targetDocumentService =
            serviceProvider.GetRequiredKeyedService<AsyncApiDocumentService>(lowercasedDocumentName);
        using var scopedService = serviceProvider.CreateScope();
        var document = await targetDocumentService.GetAsyncApiDocumentAsync(scopedService.ServiceProvider);

        // For V2, use the helper to ensure required properties are present
        // The AsyncAPI 2.x specification requires 'channels' to be present (can be empty object)
        var serialized = AsyncApiSpecVersion == AsyncApiVersion.AsyncApi2_0
            ? AsyncApiSerializationHelper.SerializeV2ToJson(document)
            : AsyncApiSerializationHelper.SerializeV3ToJson(document);

        // Build-time generation must produce the same bytes the endpoint would serve, so the
        // serialized-document transformers run here too. This is also why the V3 branch buffers via the
        // helper rather than writing straight through an AsyncApiJsonWriter as it used to.
        var documentOptions = serviceProvider.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>()
            .Get(lowercasedDocumentName);
        serialized = await AsyncApiSerializedDocumentPipeline.ApplyAsync(
            serialized,
            documentOptions,
            lowercasedDocumentName,
            AsyncApiDocumentFormat.Json,
            scopedService.ServiceProvider,
            CancellationToken.None);

        await writer.WriteAsync(serialized);
    }

    /// <summary>
    /// Maps each AsyncApi document's build-time name to its document name.
    /// </summary>
    /// <param name="openApiNames">The OpenAPI document names, empty when <c>AddOpenApi()</c> wasn't called.</param>
    /// <exception cref="InvalidOperationException">
    /// Two AsyncApi documents share a build-time name, or one uses the name of an OpenAPI document.
    /// </exception>
    private Dictionary<string, string> GetBuildTimeDocumentNames(out HashSet<string> openApiNames)
    {
        openApiNames = new HashSet<string>(
            _openApi.Value?.GetDocumentNames() ?? [],
            StringComparer.OrdinalIgnoreCase);

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>();
        var buildTimeNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var documentName in GetAsyncApiDocumentNames())
        {
            var configuredName = options.Get(documentName.ToLowerInvariant()).BuildTimeDocumentName;
            var buildTimeName = !string.IsNullOrWhiteSpace(configuredName)
                ? configuredName
                : openApiNames.Contains(documentName)
                    ? BuildTimeDocumentNamePrefix + documentName
                    : documentName;

            // A shared name would make one document silently replace the other in the generated files.
            if (openApiNames.Contains(buildTimeName))
            {
                throw new InvalidOperationException(
                    $"AsyncAPI document '{documentName}' uses the build-time name '{buildTimeName}', which is also the name of an OpenAPI document. " +
                    $"Set a different {nameof(AsyncApiOptions)}.{nameof(AsyncApiOptions.BuildTimeDocumentName)}.");
            }

            if (!buildTimeNames.TryAdd(buildTimeName, documentName))
            {
                throw new InvalidOperationException(
                    $"AsyncAPI documents '{buildTimeNames[buildTimeName]}' and '{documentName}' both use the build-time name '{buildTimeName}'. " +
                    $"Set a different {nameof(AsyncApiOptions)}.{nameof(AsyncApiOptions.BuildTimeDocumentName)} on one of them.");
            }
        }

        return buildTimeNames;
    }

    private IEnumerable<string> GetAsyncApiDocumentNames()
    {
        // Keyed services lack an API to resolve all registered keys.
        // We use the service provider to resolve an internal type.
        // This type tracks registered document names.
        // See https://github.com/dotnet/runtime/issues/100105 for more info.
        var documentServices = serviceProvider.GetServices<NamedService<AsyncApiDocumentService>>();
        var names = documentServices.Select(docService => docService.Name).ToList();

        var nameProviders = serviceProvider.GetServices<IAsyncApiDocumentNamesProvider>();
        foreach (var provider in nameProviders)
        {
            names.AddRange(provider.GetDocumentNames());
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
