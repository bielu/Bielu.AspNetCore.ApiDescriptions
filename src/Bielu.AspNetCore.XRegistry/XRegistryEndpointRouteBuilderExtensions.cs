using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Bielu.AspNetCore.XRegistry.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bielu.AspNetCore.XRegistry;

/// <summary>xRegistry methods for <see cref="IEndpointRouteBuilder"/>.</summary>
public static class XRegistryEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Serves the messages and payload schemas of the app's AsyncAPI documents as a read-only CNCF xRegistry
    /// (1.0-rc4). Each AsyncAPI document becomes a message group and a schema group with the document's name: every
    /// message under <c>components/messages</c> is a message definition, and its payload schema is a schema the message
    /// points at with <c>dataschemaxid</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The registry answers <c>GET</c> on the registry, <c>/capabilities</c>, <c>/model</c>, the group, resource,
    /// <c>meta</c> and Version collections and entities, with the <c>?inline</c> flag. A schema's URL returns the schema
    /// document itself with its attributes as <c>xRegistry-</c> headers, and <c>$details</c> returns its metadata. Payload
    /// schemas are JSON Schema (draft-07, standalone with <c>definitions</c>), Avro or Protobuf, as the AsyncAPI
    /// document declares them.
    /// </para>
    /// <para>
    /// The documents are read as build-time generation writes them, so overlays and other serialized-document
    /// transformers have been applied. Every entity has <c>epoch</c> 1 and one Version, <c>1</c>.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/>.</param>
    /// <param name="prefix">The root of the registry. Defaults to <see cref="XRegistryDefaults.DefaultRoute"/>.</param>
    /// <param name="mapWellKnownDiscovery">
    /// Whether to also serve <see cref="XRegistryDefaults.WellKnownRoute"/>, listing this registry for host-based discovery.
    /// </param>
    /// <returns>An <see cref="IEndpointConventionBuilder"/> to further customize the registry's endpoints, e.g. <c>.RequireAuthorization()</c>.</returns>
    /// <example>
    /// <code>
    /// app.MapAsyncApi();
    /// app.MapXRegistry(); // GET /registry, /registry/messagegroups/v1/messages, …
    /// </code>
    /// </example>
    public static IEndpointConventionBuilder MapXRegistry(this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string prefix = XRegistryDefaults.DefaultRoute, bool mapWellKnownDiscovery = true)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var root = "/" + prefix.Trim('/');
        var createdAt = DateTimeOffset.UtcNow;

        // RequestDelegates rather than parameter-bound lambdas, which would go through RequestDelegateFactory's
        // reflection-based binding and fail under Native AOT.
        RequestDelegate handler = context => XRegistryRequestHandler.HandleAsync(context, root, createdAt);
        var group = endpoints.MapGroup(root);
        group.MapGet(string.Empty, handler);
        group.MapGet("/{**path}", handler);

        if (mapWellKnownDiscovery)
        {
            endpoints.MapGet(XRegistryDefaults.WellKnownRoute, (RequestDelegate)(context => WriteDiscoveryAsync(context, root)))
                .ExcludeFromDescription();
        }

        return group.ExcludeFromDescription();
    }

    private static async Task WriteDiscoveryAsync(HttpContext context, string root)
    {
        var request = context.Request;
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new XRegistryDiscovery([$"{request.Scheme}://{request.Host}{request.PathBase}{root}"]),
            XRegistryJsonContext.Default.XRegistryDiscovery);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength = payload.Length;
        await context.Response.Body.WriteAsync(payload, context.RequestAborted);
    }
}
