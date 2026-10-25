using System.Diagnostics.CodeAnalysis;
using Bielu.AspNetCore.ApiDescriptions.Mcp;
using Bielu.AspNetCore.ApiDescriptions.Mcp.Tools;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Exposes the app's API descriptions on a Model Context Protocol server.</summary>
[Experimental(DiagnosticId, UrlFormat = "https://apidescriptions.bielu.pl/articles/mcp.html")]
public static class McpServerBuilderApiDescriptionsExtensions
{
    /// <summary>The diagnostic id reported for using this experimental API.</summary>
    public const string DiagnosticId = "BIELUMCP001";

    /// <summary>
    /// Lets AI agents connected to this MCP server read the app's own API descriptions: every AsyncAPI, OpenAPI and
    /// Arazzo document it serves, as the resource <c>apidescription://{kind}/{name}</c>, and six read-only tools:
    /// <list type="bullet">
    /// <item><c>list_api_descriptions</c> lists the documents, their URIs, titles, specification versions and the tools
    /// that can inspect them;</item>
    /// <item><c>get_api_description</c> returns one document;</item>
    /// <item><c>find_api_operations</c> searches the operations of every AsyncAPI document and returns a handle for
    /// each match;</item>
    /// <item><c>get_api_operation</c> returns the complete documented contract of one operation: channel, servers,
    /// security, messages and schemas, bindings, reply and examples, with what the document does not specify listed;</item>
    /// <item><c>list_asyncapi_operations</c> summarizes what an AsyncAPI document says the app sends and receives;</item>
    /// <item><c>get_asyncapi_message</c> returns one message with every part of the document it references.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// The documents are written as build-time generation writes them, overlays included. Nothing here changes the app
    /// or calls its APIs. They add to whatever other tools and resources the server has, and the transport (for example
    /// <c>WithHttpTransport()</c> and <c>MapMcp()</c> from <c>ModelContextProtocol.AspNetCore</c>) is configured as usual.
    /// The API is experimental: tool names and outputs may change.
    /// </remarks>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The <paramref name="builder"/>, for chaining.</returns>
    /// <example>
    /// <code>
    /// #pragma warning disable BIELUMCP001
    /// builder.Services.AddMcpServer()
    ///     .WithHttpTransport()
    ///     .WithApiDescriptions();
    /// #pragma warning restore BIELUMCP001
    ///
    /// app.MapMcp("/mcp");
    /// </code>
    /// </example>
    public static IMcpServerBuilder WithApiDescriptions(this IMcpServerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithResources([new ApiDescriptionResource()])
            .WithTools(
            [
                new ListApiDescriptionsTool(),
                new GetApiDescriptionTool(),
                new FindApiOperationsTool(),
                new GetApiOperationTool(),
                new ListAsyncApiOperationsTool(),
                new GetAsyncApiMessageTool(),
            ]);
    }
}
