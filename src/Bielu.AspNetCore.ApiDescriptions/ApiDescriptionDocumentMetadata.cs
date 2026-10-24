namespace Bielu.AspNetCore.ApiDescriptions;

/// <summary>
/// Endpoint metadata marking an endpoint that serves API description documents, such as the one
/// <c>MapAsyncApi()</c> or <c>MapArazzo()</c> maps, so the app can list the documents it serves.
/// </summary>
/// <remarks>
/// When the endpoint's route has a <c>documentName</c> parameter, it serves every document of the
/// <see cref="IBuildTimeDocumentSource"/> whose <see cref="IBuildTimeDocumentSource.Kind"/> is <see cref="Kind"/>.
/// </remarks>
/// <example>
/// <code>
/// app.MapGet("/workflows/{documentName}.json", handler)
///     .WithMetadata(new ApiDescriptionDocumentMetadata("Workflow", "application/json"));
/// </code>
/// </example>
public sealed class ApiDescriptionDocumentMetadata
{
    /// <summary>Creates the metadata for an endpoint serving documents of <paramref name="kind"/>.</summary>
    /// <param name="kind">The kind of document served, matching an <see cref="IBuildTimeDocumentSource.Kind"/>.</param>
    /// <param name="mediaType">The media type of the documents served, for example <c>application/vnd.aai.asyncapi+json</c>.</param>
    public ApiDescriptionDocumentMetadata(string kind, string mediaType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

        Kind = kind;
        MediaType = mediaType;
    }

    /// <summary>The kind of document served, for example <c>AsyncAPI</c>.</summary>
    public string Kind { get; }

    /// <summary>The media type of the documents served.</summary>
    public string MediaType { get; }
}
