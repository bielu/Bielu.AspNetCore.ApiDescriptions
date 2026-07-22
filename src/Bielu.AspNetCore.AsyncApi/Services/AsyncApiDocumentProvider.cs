// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Writers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.ApiDescriptions;

/// <summary>
/// Provides an implementation of <see cref="IDocumentProvider"/> to use for build-time generation of AsyncApi documents.
/// </summary>
/// <param name="serviceProvider">The <see cref="IServiceProvider"/> to use.</param>
internal sealed class AsyncApiDocumentProvider(IServiceProvider serviceProvider) : IDocumentProvider
{
    /// <summary>
    /// Serializes the AsyncApi document associated with a given document name to
    /// the provided writer.
    /// </summary>
    /// <param name="documentName">The name of the document to resolve.</param>
    /// <param name="writer">A text writer associated with the document to write to.</param>
    public async Task GenerateAsync(string documentName, TextWriter writer)
    {
        // See AsyncApiServiceCollectionExtensions.cs to learn why we lowercase the document name
        var lowercasedDocumentName = documentName.ToLowerInvariant();

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>();
        var namedOption = options.Get(lowercasedDocumentName);
        var resolvedAsyncApiVersion = namedOption.AsyncApiVersion;
        await GenerateAsync(lowercasedDocumentName, writer, resolvedAsyncApiVersion);
    }

    /// <summary>
    /// Serializes the AsyncApi document associated with a given document name to
    /// the provided writer under the provided AsyncApi spec version.
    /// </summary>
    /// <param name="documentName">The name of the document to resolve.</param>
    /// <param name="writer">A text writer associated with the document to write to.</param>
    /// <param name="AsyncApiSpecVersion">The AsyncApi specification version to use when serializing the document.</param>
    public async Task GenerateAsync(string documentName, TextWriter writer, AsyncApiVersion AsyncApiSpecVersion)
    {
        // We need to retrieve the document name in a case-insensitive manner to support case-insensitive document name resolution.
        // The document service is registered with a key equal to the document name, but in lowercase.
        // The GetRequiredKeyedService() method is case-sensitive, which doesn't work well for AsyncApi document names here,
        // as the document name is also used as the route to retrieve the document, so we need to ensure this is lowercased to achieve consistency with ASP.NET Core routing.
        // See AsyncApiServiceCollectionExtensions.cs for more info.
        var lowercasedDocumentName = documentName.ToLowerInvariant();

        var targetDocumentService = serviceProvider.GetRequiredKeyedService<AsyncApiDocumentService>(lowercasedDocumentName);
        using var scopedService = serviceProvider.CreateScope();
        var document = await targetDocumentService.GetAsyncApiDocumentAsync(scopedService.ServiceProvider);

        // For V2, use the helper to ensure required properties are present
        // The AsyncAPI 2.x specification requires 'channels' to be present (can be empty object)
        if (AsyncApiSpecVersion == AsyncApiVersion.AsyncApi2_0)
        {
            var serialized = AsyncApiSerializationHelper.SerializeV2ToJson(document);
            await writer.WriteAsync(serialized);
        }
        else
        {
            var jsonWriter = new AsyncApiJsonWriter(writer);
            document.SerializeV3(jsonWriter);
        }
    }

    /// <summary>
    /// Provides all document names that are currently managed in the application.
    /// </summary>
    public IEnumerable<string> GetDocumentNames()
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
