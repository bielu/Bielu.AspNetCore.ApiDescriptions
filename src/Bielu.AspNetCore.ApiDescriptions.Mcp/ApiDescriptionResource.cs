using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp;

/// <summary>
/// The app's API description documents as one templated MCP resource, <c>apidescription://{kind}/{name}</c>, so the
/// documents need not be known when the server is configured. The <c>list_api_descriptions</c> tool lists their URIs.
/// </summary>
internal sealed class ApiDescriptionResource : McpServerResource
{
    private const string UriPrefix = ApiDescriptionDocument.UriScheme + "://";

    public override ResourceTemplate ProtocolResourceTemplate { get; } = new()
    {
        Name = "api-description",
        Title = "API description",
        UriTemplate = UriPrefix + "{kind}/{name}",
        Description = "An API description document this app serves: kind is asyncapi, openapi or arazzo, name the document " +
                      "name. Call list_api_descriptions for the available URIs.",
        MimeType = "application/json",
    };

    public override IReadOnlyList<object> Metadata { get; } = [];

    public override bool IsMatch(string uri) =>
        uri.StartsWith(UriPrefix, StringComparison.OrdinalIgnoreCase)
        && uri[UriPrefix.Length..].Split('/') is [{ Length: > 0 }, { Length: > 0 }];

    public override async ValueTask<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var uri = request.Params?.Uri ?? string.Empty;
        var catalog = new ApiDescriptionCatalog(request.Services ?? request.Server.Services!);
        var document = catalog.FindByUri(uri)
                       ?? throw new McpProtocolException($"No API description has the URI '{uri}'.", McpErrorCode.InvalidParams);

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents { Uri = document.Uri, MimeType = document.MimeType, Text = await catalog.GenerateAsync(document) },
            ],
        };
    }
}
