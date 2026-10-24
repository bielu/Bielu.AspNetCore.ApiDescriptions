using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using static Bielu.AspNetCore.AsyncApi.XRegistry.Internal.XRegistryEntityWriter;

namespace Bielu.AspNetCore.AsyncApi.XRegistry.Internal;

/// <summary>Answers the read-only xRegistry HTTP API: the entity, collection, meta and Version reads.</summary>
internal static class XRegistryRequestHandler
{
    private const string Details = "$details";
    private const string JsonContentType = "application/json; charset=utf-8";
    private const string ErrorTypeBase = "https://github.com/xregistry/spec/blob/main/core/spec.md#";

    /// <summary>The flags of the specification this registry does not support; using one is an error.</summary>
    private static readonly string[] UnsupportedFlags =
        ["binary", "collections", "doc", "epoch", "filter", "ignore", "setdefaultversionid", "sort", "specversion"];

    private static readonly string[] MessageResourcePaths = ["meta", "versions"];
    private static readonly string[] SchemaResourcePaths = ["schema", "meta", "versions", "versions.schema"];
    private static readonly string[] SchemaVersionPaths = ["schema"];
    private static readonly string[] RootPaths = BuildRootPaths();

    public static async Task HandleAsync(HttpContext context, string prefix, DateTimeOffset createdAt)
    {
        var request = context.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}{prefix}";
        var segments = (request.Path.Value ?? string.Empty)[prefix.Length..]
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();

        if (UnsupportedFlags.FirstOrDefault(flag => request.Query.ContainsKey(flag)) is { } flag)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "bad_flag",
                $"The specified flag ({flag}) is not allowed in this context: {request.Path}.", request.Path, ("flag", flag));
            return;
        }

        var options = context.RequestServices.GetRequiredService<IOptions<XRegistryOptions>>().Value;
        var snapshot = await AsyncApiRegistryReader.ReadAsync(context.RequestServices, options);

        var route = Route.Parse(segments);
        if (route is null || !Resolve(route, snapshot, out var group))
        {
            await WriteNotFoundAsync(context, "/" + string.Join('/', segments));
            return;
        }

        var inline = InlineSelection.None;
        if (request.Query.TryGetValue("inline", out var inlineValues))
        {
            var parsed = InlineSelection.Parse(inlineValues, ValidInlinePaths(route), out var invalid);
            if (parsed is null)
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "bad_inline",
                    $"For \"{request.Path}\", an error was found in \"inline\" value ({invalid}): unknown or non-inlineable attribute.",
                    request.Path, ("value", invalid!), ("error_detail", "unknown or non-inlineable attribute"));
                return;
            }

            inline = parsed;
        }

        var writer = new XRegistryEntityWriter(baseUrl, createdAt, inline);
        if (route.IsSchemaDocument)
        {
            await WriteSchemaDocumentAsync(context, writer, group!, route);
            return;
        }

        await WriteJsonAsync(context, json => WriteEntity(json, writer, route, snapshot, group, options),
            route.DefaultVersionLocation(writer));
    }

    private static void WriteEntity(Utf8JsonWriter json, XRegistryEntityWriter writer, Route route,
        RegistrySnapshot snapshot, RegistryGroup? group, XRegistryOptions options)
    {
        var isMessages = route.Groups == MessageGroups;
        switch (route.Kind)
        {
            case RouteKind.Registry:
                writer.WriteRegistry(json, snapshot, options);
                break;
            case RouteKind.Capabilities:
                XRegistryStaticDocuments.WriteCapabilities(json);
                break;
            case RouteKind.Model:
                XRegistryStaticDocuments.WriteModel(json);
                break;
            case RouteKind.Groups:
                WriteMap(json, snapshot.Groups, g => g.Id, g => WriteGroup(json, writer, g, isMessages));
                break;
            case RouteKind.Group:
                WriteGroup(json, writer, group!, isMessages);
                break;
            case RouteKind.Resources when isMessages:
                WriteMap(json, group!.Messages, m => m.Id, m => writer.WriteMessage(json, group, m, string.Empty));
                break;
            case RouteKind.Resources:
                WriteMap(json, group!.Schemas, s => s.Id, s => writer.WriteSchema(json, group, s, string.Empty));
                break;
            case RouteKind.Resource when isMessages:
                writer.WriteMessage(json, group!, group!.FindMessage(route.ResourceId!)!, string.Empty);
                break;
            case RouteKind.Resource:
                writer.WriteSchema(json, group!, group!.FindSchema(route.ResourceId!)!, string.Empty);
                break;
            case RouteKind.Meta:
                writer.WriteMeta(json, isMessages ? "messageid" : "schemaid", route.ResourceId!,
                    ResourceXid(route.Groups!, group!.Id, route.Resources!, route.ResourceId!), hasDocument: !isMessages);
                break;
            case RouteKind.Versions:
                json.WriteStartObject();
                json.WritePropertyName(VersionId);
                WriteVersion(json, writer, route, group!, isMessages);
                json.WriteEndObject();
                break;
            case RouteKind.Version:
                WriteVersion(json, writer, route, group!, isMessages);
                break;
        }
    }

    private static void WriteGroup(Utf8JsonWriter json, XRegistryEntityWriter writer, RegistryGroup group, bool isMessages)
    {
        if (isMessages)
        {
            writer.WriteMessageGroup(json, group, string.Empty);
        }
        else
        {
            writer.WriteSchemaGroup(json, group, string.Empty);
        }
    }

    private static void WriteVersion(Utf8JsonWriter json, XRegistryEntityWriter writer, Route route, RegistryGroup group, bool isMessages)
    {
        if (isMessages)
        {
            writer.WriteMessageVersion(json, group, group.FindMessage(route.ResourceId!)!);
        }
        else
        {
            writer.WriteSchemaVersion(json, group, group.FindSchema(route.ResourceId!)!, string.Empty);
        }
    }

    /// <summary>
    /// Writes a schema's document, the exact bytes, with its default Version's attributes as <c>xRegistry-</c> headers.
    /// </summary>
    private static async Task WriteSchemaDocumentAsync(HttpContext context, XRegistryEntityWriter writer, RegistryGroup group, Route route)
    {
        var schema = group.FindSchema(route.ResourceId!)!;
        var resourceXid = ResourceXid(SchemaGroups, group.Id, Schemas, schema.Id);
        var versionXid = resourceXid + "/versions/" + VersionId;
        var headers = context.Response.Headers;

        headers["xRegistry-schemaid"] = schema.Id;
        headers["xRegistry-versionid"] = VersionId;
        headers["xRegistry-self"] = writer.Url(route.Kind == RouteKind.Version ? versionXid : resourceXid) + Details;
        headers["xRegistry-xid"] = route.Kind == RouteKind.Version ? versionXid : resourceXid;
        headers["xRegistry-epoch"] = "1";
        if (schema.Name is not null)
        {
            headers["xRegistry-name"] = EncodeHeaderValue(schema.Name);
        }

        headers["xRegistry-isdefault"] = "true";
        headers["xRegistry-createdat"] = writer.Timestamp;
        headers["xRegistry-modifiedat"] = writer.Timestamp;
        headers["xRegistry-ancestorid"] = VersionId;
        headers["xRegistry-format"] = EncodeHeaderValue(schema.Format);
        if (route.Kind == RouteKind.Resource)
        {
            headers["xRegistry-metaurl"] = writer.Url(resourceXid + "/meta");
            headers["xRegistry-versionsurl"] = writer.Url(resourceXid + "/versions");
            headers["xRegistry-versionscount"] = "1";
            headers[HeaderNames.ContentLocation] = writer.Url(versionXid);
        }

        headers[HeaderNames.ContentDisposition] = $"inline; filename=\"{schema.Id}\"";
        context.Response.ContentType = schema.ContentType;
        context.Response.ContentLength = schema.Document.Length;
        await context.Response.Body.WriteAsync(schema.Document, context.RequestAborted);
    }

    private static async Task WriteJsonAsync(HttpContext context, Action<Utf8JsonWriter> write, string? contentLocation)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            write(json);
        }

        if (contentLocation is not null)
        {
            context.Response.Headers[HeaderNames.ContentLocation] = contentLocation;
        }

        context.Response.ContentType = JsonContentType;
        context.Response.ContentLength = buffer.WrittenCount;
        await context.Response.Body.WriteAsync(buffer.WrittenMemory, context.RequestAborted);
    }

    private static Task WriteNotFoundAsync(HttpContext context, string xid) =>
        WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found",
            $"The targeted entity ({xid}) cannot be found.", xid);

    /// <summary>Writes an RFC 9457 problem in the form the xRegistry HTTP binding defines.</summary>
    private static async Task WriteErrorAsync(HttpContext context, int status, string type, string title, string subject,
        params (string Name, string Value)[] args)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("type", ErrorTypeBase + type);
            json.WriteString("title", title);
            json.WriteString("subject", subject);
            if (args.Length > 0)
            {
                json.WriteStartObject("args");
                foreach (var (name, value) in args)
                {
                    json.WriteString(name, value);
                }

                json.WriteEndObject();
            }

            json.WriteString("instance", $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}");
            json.WriteEndObject();
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = JsonContentType;
        context.Response.ContentLength = buffer.WrittenCount;
        await context.Response.Body.WriteAsync(buffer.WrittenMemory, context.RequestAborted);
    }

    /// <summary>
    /// Encodes a header value as the HTTP binding asks: non-US-ASCII characters, <c>%</c> and <c>"</c> are percent-encoded.
    /// </summary>
    internal static string EncodeHeaderValue(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            if (b is < 0x20 or > 0x7E or (byte)'%' or (byte)'"')
            {
                builder.Append('%').Append(b.ToString("X2"));
            }
            else
            {
                builder.Append((char)b);
            }
        }

        return builder.ToString();
    }

    private static bool Resolve(Route route, RegistrySnapshot snapshot, out RegistryGroup? group)
    {
        group = route.GroupId is null ? null : snapshot.FindGroup(route.GroupId);
        if (route.GroupId is not null && group is null)
        {
            return false;
        }

        return route.ResourceId is null
               || (route.Groups == MessageGroups
                   ? group!.FindMessage(route.ResourceId) is not null
                   : group!.FindSchema(route.ResourceId) is not null)
               && (route.Version is null || route.Version == VersionId);
    }

    private static IReadOnlyCollection<string> ValidInlinePaths(Route route) => route.Kind switch
    {
        RouteKind.Registry => RootPaths,
        RouteKind.Groups or RouteKind.Group => route.Groups == MessageGroups
            ? [Messages, .. MessageResourcePaths.Select(path => $"{Messages}.{path}")]
            : [Schemas, .. SchemaResourcePaths.Select(path => $"{Schemas}.{path}")],
        RouteKind.Resources or RouteKind.Resource => route.Groups == MessageGroups ? MessageResourcePaths : SchemaResourcePaths,
        RouteKind.Versions or RouteKind.Version when route.Groups == SchemaGroups => SchemaVersionPaths,
        _ => [],
    };

    private static string[] BuildRootPaths() =>
    [
        "model", "capabilities",
        MessageGroups, $"{MessageGroups}.{Messages}",
        .. MessageResourcePaths.Select(path => $"{MessageGroups}.{Messages}.{path}"),
        SchemaGroups, $"{SchemaGroups}.{Schemas}",
        .. SchemaResourcePaths.Select(path => $"{SchemaGroups}.{Schemas}.{path}"),
    ];

    private enum RouteKind
    {
        Registry,
        Capabilities,
        Model,
        Groups,
        Group,
        Resources,
        Resource,
        Meta,
        Versions,
        Version,
    }

    /// <summary>The entity a request path points at.</summary>
    private sealed record Route(RouteKind Kind, string? Groups = null, string? GroupId = null, string? Resources = null,
        string? ResourceId = null, string? Version = null, bool WantsDetails = false)
    {
        /// <summary>Whether the request is for a schema's document rather than its xRegistry metadata.</summary>
        public bool IsSchemaDocument =>
            Groups == SchemaGroups && Kind is RouteKind.Resource or RouteKind.Version && !WantsDetails;

        /// <summary>The <c>Content-Location</c> of a Resource's metadata: its default Version.</summary>
        public string? DefaultVersionLocation(XRegistryEntityWriter writer) => Kind == RouteKind.Resource
            ? writer.Url(ResourceXid(Groups!, GroupId!, Resources!, ResourceId!) + "/versions/" + VersionId + (Groups == SchemaGroups ? Details : string.Empty))
            : null;

        public static Route? Parse(string[] segments)
        {
            switch (segments)
            {
                case []:
                    return new Route(RouteKind.Registry);
                case ["capabilities"]:
                    return new Route(RouteKind.Capabilities);
                case ["model"]:
                    return new Route(RouteKind.Model);
            }

            if (segments[0] is not (MessageGroups or SchemaGroups))
            {
                return null;
            }

            var groups = segments[0];
            var resources = groups == MessageGroups ? Messages : Schemas;
            return segments switch
            {
                [_] => new Route(RouteKind.Groups, groups),
                [_, var gid] => new Route(RouteKind.Group, groups, gid),
                [_, var gid, var res] when res == resources => new Route(RouteKind.Resources, groups, gid, resources),
                [_, var gid, var res, var rid] when res == resources => WithDetails(rid, details =>
                    new Route(RouteKind.Resource, groups, gid, resources, details.Id, WantsDetails: details.Wants)),
                [_, var gid, var res, var rid, "meta"] when res == resources => new Route(RouteKind.Meta, groups, gid, resources, rid),
                [_, var gid, var res, var rid, "versions"] when res == resources => new Route(RouteKind.Versions, groups, gid, resources, rid),
                [_, var gid, var res, var rid, "versions", var vid] when res == resources => WithDetails(vid, details =>
                    new Route(RouteKind.Version, groups, gid, resources, rid, details.Id, details.Wants)),
                _ => null,
            };
        }

        private static Route WithDetails(string segment, Func<(string Id, bool Wants), Route> create) =>
            segment.EndsWith(Details, StringComparison.Ordinal)
                ? create((segment[..^Details.Length], true))
                : create((segment, false));
    }
}
