using Bielu.Arazzo.Models;

namespace Bielu.AspNetCore.Arazzo.Services;

/// <summary>Options for a single Arazzo document, configured via <c>AddArazzo</c>.</summary>
public sealed class ArazzoOptions
{
    /// <summary>The name of this document, used in the <c>MapArazzo</c> route and for keyed-service lookup.</summary>
    public string DocumentName { get; internal set; } = ArazzoDefaults.DefaultDocumentName;

    /// <summary>The document's <c>info</c> object. Set via <see cref="WithInfo(string,string)"/>.</summary>
    public ArazzoInfo? Info { get; set; }

    /// <summary>
    /// When true (the default), every workflow step's <c>operationId</c>/<c>operationPath</c>/<c>channelPath</c>
    /// is resolved against the live self-wired AsyncAPI/OpenAPI documents at app startup, so a renamed
    /// channel or operation fails startup instead of failing in production.
    /// </summary>
    public bool ValidateSourceReferencesOnStartup { get; set; } = true;

    internal List<ArazzoSourceDescription> SourceDescriptions { get; } = [];

    internal List<ArazzoWorkflow> Workflows { get; } = [];

    internal List<SourceWiring> SourceWirings { get; } = [];

    /// <summary>Sets the document's title and version.</summary>
    public ArazzoOptions WithInfo(string title, string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentException.ThrowIfNullOrEmpty(version);
        Info = new ArazzoInfo { Title = title, Version = version };
        return this;
    }

    /// <summary>Configures the document's <c>info</c> object.</summary>
    public ArazzoOptions WithInfo(Action<ArazzoInfo> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Info ??= new ArazzoInfo { Title = DocumentName, Version = "1.0.0" };
        configure(Info);
        return this;
    }

    /// <summary>Adds a source description pointing at an arbitrary URL. Prefer <see cref="AddAsyncApiSource"/>/<see cref="AddOpenApiSource"/> to self-wire against a document served by this same app.</summary>
    public ArazzoOptions AddSourceDescription(string name, string url, string type)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(url);
        ArgumentException.ThrowIfNullOrEmpty(type);
        SourceDescriptions.Add(new ArazzoSourceDescription { Name = name, Url = url, Type = type });
        return this;
    }

    /// <summary>
    /// Adds a source description of type <c>asyncapi</c> that self-wires against the AsyncAPI document named
    /// <paramref name="asyncApiDocumentName"/> served by this same app (via <c>Bielu.AspNetCore.AsyncApi</c>'s
    /// <c>AddAsyncApi</c>/<c>MapAsyncApi</c>). When <see cref="ValidateSourceReferencesOnStartup"/> is true,
    /// this document's live, in-memory contents are used to validate every step referencing this source.
    /// </summary>
    /// <param name="sourceName">The name this source is referred to as within workflow steps (<c>sourceDescriptions.NAME</c>).</param>
    /// <param name="asyncApiDocumentName">The document name passed to <c>AddAsyncApi</c>/<c>MapAsyncApi</c> for the target document.</param>
    /// <param name="url">The externally reachable URL the source description should advertise. Defaults to <c>/asyncapi/{asyncApiDocumentName}.json</c>.</param>
    public ArazzoOptions AddAsyncApiSource(string sourceName, string asyncApiDocumentName, string? url = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        ArgumentException.ThrowIfNullOrEmpty(asyncApiDocumentName);
        var lowercasedDocumentName = asyncApiDocumentName.ToLowerInvariant();
        AddSourceDescription(sourceName, url ?? $"/asyncapi/{lowercasedDocumentName}.json",
            ArazzoSourceDescriptionType.AsyncApi);
        SourceWirings.Add(new SourceWiring(sourceName, ArazzoSourceDescriptionType.AsyncApi, lowercasedDocumentName));
        return this;
    }

    /// <summary>
    /// Adds a source description of type <c>openapi</c> that self-wires against the OpenAPI document named
    /// <paramref name="openApiDocumentName"/> served by this same app (via <c>Microsoft.AspNetCore.OpenApi</c>'s
    /// <c>AddOpenApi</c>/<c>MapOpenApi</c>). When <see cref="ValidateSourceReferencesOnStartup"/> is true,
    /// this document's live, in-memory contents are used to validate every step referencing this source.
    /// </summary>
    /// <param name="sourceName">The name this source is referred to as within workflow steps (<c>sourceDescriptions.NAME</c>).</param>
    /// <param name="openApiDocumentName">The document name passed to <c>AddOpenApi</c>/<c>MapOpenApi</c> for the target document.</param>
    /// <param name="url">The externally reachable URL the source description should advertise. Defaults to <c>/openapi/{openApiDocumentName}.json</c>.</param>
    public ArazzoOptions AddOpenApiSource(string sourceName, string openApiDocumentName, string? url = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        ArgumentException.ThrowIfNullOrEmpty(openApiDocumentName);
        AddSourceDescription(sourceName, url ?? $"/openapi/{openApiDocumentName}.json",
            ArazzoSourceDescriptionType.OpenApi);
        SourceWirings.Add(new SourceWiring(sourceName, ArazzoSourceDescriptionType.OpenApi, openApiDocumentName));
        return this;
    }

    /// <summary>Adds a workflow to the document.</summary>
    public ArazzoOptions AddWorkflow(string workflowId, Action<ArazzoWorkflowBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ArazzoWorkflowBuilder(workflowId);
        configure(builder);
        Workflows.Add(builder.Build());
        return this;
    }

    /// <summary>Associates a self-wired source description with the document name it resolves against at runtime.</summary>
    internal readonly record struct SourceWiring(string SourceName, string SourceType, string DocumentName);
}
