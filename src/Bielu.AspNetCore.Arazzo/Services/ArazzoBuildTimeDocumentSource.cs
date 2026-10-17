using Bielu.Arazzo;
using Bielu.Arazzo.Writers;
using Bielu.AspNetCore.ApiDescriptions;
using Bielu.AspNetCore.Arazzo.Transformers;
using Bielu.AspNetCore.Arazzo.Validation;
using Bielu.AspNetCore.AsyncApi.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bielu.AspNetCore.Arazzo.Services;

/// <summary>
/// Lists and writes the app's Arazzo documents for build-time generation (<c>dotnet getdocument</c>), through the
/// provider <c>Bielu.AspNetCore.ApiDescriptions</c> exposes to the tool.
/// </summary>
/// <remarks>
/// The output is the JSON the <c>MapArazzo</c> endpoint would serve, including serialized-document transformers
/// such as overlays. The tool boots the app, so the startup validation of self-wired source references runs first
/// and a stale reference fails the build.
/// </remarks>
internal sealed class ArazzoBuildTimeDocumentSource(
    IServiceProvider serviceProvider,
    IEnumerable<NamedArazzoDocument> documents,
    IOptionsMonitor<ArazzoOptions> optionsMonitor) : IBuildTimeDocumentSource
{
    public string Kind => "Arazzo";

    public string NamePrefix => "arazzo-";

    // Named after AsyncAPI (order 0), so an Arazzo document colliding with an AsyncAPI one is the one prefixed.
    public int Order => 100;

    public IEnumerable<BuildTimeDocument> GetDocuments() =>
        documents.Select(d => d.DocumentName).Distinct(StringComparer.Ordinal).Select(name =>
        {
            var configured = optionsMonitor.Get(name).BuildTimeDocumentName;
            return new BuildTimeDocument(name, string.IsNullOrWhiteSpace(configured) ? null : configured);
        });

    public async Task GenerateAsync(string documentName, TextWriter writer)
    {
        // See AddArazzo for why document names are lowercased: they double as case-sensitive keys.
        var lowercasedDocumentName = documentName.ToLowerInvariant();

        var document = await serviceProvider
            .GetRequiredKeyedService<IArazzoDocumentProvider>(lowercasedDocumentName)
            .GetArazzoDocumentAsync();

        var serialized = ArazzoJsonWriter.Write(document);
        serialized = await ArazzoSerializedDocumentPipeline.ApplyAsync(
            serialized,
            optionsMonitor.Get(lowercasedDocumentName),
            lowercasedDocumentName,
            ArazzoDocumentFormat.Json,
            serviceProvider,
            CancellationToken.None);

        await writer.WriteAsync(serialized);
    }
}
