// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.ApiDescriptions;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bielu.AspNetCore.AsyncApi.Services;

/// <summary>
/// Writes the AsyncApi documents during build-time generation (<c>dotnet getdocument</c>).
/// </summary>
/// <remarks>
/// AsyncAPI documents are named right after the OpenAPI ones, so an AsyncAPI <c>v1</c> next to an OpenAPI <c>v1</c>
/// is listed as <c>asyncapi-v1</c> unless <see cref="AsyncApiOptions.BuildTimeDocumentName"/> says otherwise.
/// </remarks>
/// <param name="serviceProvider">The <see cref="IServiceProvider"/> to use.</param>
internal sealed class AsyncApiBuildTimeDocumentSource(IServiceProvider serviceProvider) : IBuildTimeDocumentSource
{
    /// <summary>
    /// The prefix given to an AsyncAPI document's build-time name when an OpenAPI document has the same name.
    /// </summary>
    internal const string BuildTimeDocumentNamePrefix = "asyncapi-";

    /// <summary>The <see cref="Kind"/> of AsyncAPI documents, also stamped on the <c>MapAsyncApi()</c> endpoint.</summary>
    internal const string DocumentKind = "AsyncAPI";

    /// <inheritdoc />
    public string Kind => DocumentKind;

    /// <inheritdoc />
    public string NamePrefix => BuildTimeDocumentNamePrefix;

    /// <inheritdoc />
    public int Order => 0;

    /// <summary>
    /// Lists every AsyncApi document with the <see cref="AsyncApiOptions.BuildTimeDocumentName"/> configured on it.
    /// </summary>
    public IEnumerable<BuildTimeDocument> GetDocuments()
    {
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>();
        return GetAsyncApiDocumentNames()
            .Select(documentName => new BuildTimeDocument(
                documentName,
                options.Get(documentName.ToLowerInvariant()).BuildTimeDocumentName))
            .ToList();
    }

    /// <summary>
    /// Serializes the AsyncApi document associated with a given document name to the provided writer, under the
    /// AsyncApi version configured on it.
    /// </summary>
    /// <param name="documentName">The name of the document to resolve.</param>
    /// <param name="writer">A text writer associated with the document to write to.</param>
    public Task GenerateAsync(string documentName, TextWriter writer)
    {
        // See AsyncApiServiceCollectionExtensions.cs to learn why we lowercase the document name
        var lowercasedDocumentName = documentName.ToLowerInvariant();

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>();
        var namedOption = options.Get(lowercasedDocumentName);
        var resolvedAsyncApiVersion = namedOption.AsyncApiVersion;
        return GenerateAsync(lowercasedDocumentName, writer, resolvedAsyncApiVersion);
    }

    /// <summary>
    /// Serializes the AsyncApi document associated with a given document name to
    /// the provided writer under the provided AsyncApi spec version.
    /// </summary>
    /// <param name="documentName">The name of the document to resolve.</param>
    /// <param name="writer">A text writer associated with the document to write to.</param>
    /// <param name="AsyncApiSpecVersion">The AsyncApi specification version to use when serializing the document.</param>
    internal async Task GenerateAsync(string documentName, TextWriter writer, AsyncApiVersion AsyncApiSpecVersion)
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
