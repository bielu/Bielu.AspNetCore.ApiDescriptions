using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bielu.Arazzo.Writers;
using Bielu.AspNetCore.Arazzo.Schemas;
using Bielu.AspNetCore.Arazzo.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Bielu.AspNetCore.Arazzo.Extensions;

/// <summary>Arazzo-related methods for <see cref="IEndpointRouteBuilder"/>.</summary>
public static class ArazzoEndpointRouteBuilderExtensions
{
    /// <summary>Registers an endpoint for resolving the Arazzo document associated with the current application.</summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/>.</param>
    /// <param name="pattern">The route to register the endpoint on. Must include the 'documentName' route parameter.</param>
    /// <returns>An <see cref="IEndpointConventionBuilder"/> that can be used to further customize the endpoint, e.g. <c>.RequireAuthorization()</c>.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification =
            "The handler delegate has two simple, non-generic parameters (HttpContext, string); RequestDelegateFactory's reflection-based binding for this shape is trim-safe in practice.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification =
            "The handler delegate has two simple, non-generic parameters (HttpContext, string); RequestDelegateFactory's reflection-based binding for this shape does not require runtime code generation.")]
    public static IEndpointConventionBuilder MapArazzo(this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern = ArazzoDefaults.DefaultArazzoRoute)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapGet(pattern,
                (HttpContext context, string documentName = ArazzoDefaults.DefaultDocumentName) =>
                    HandleAsync(context, documentName, pattern))
            .ExcludeFromDescription();
    }

    private static async Task HandleAsync(HttpContext context, string documentName, string pattern)
    {
        var lowercasedDocumentName = documentName.ToLowerInvariant();
        var documentProvider = context.RequestServices.GetKeyedService<IArazzoDocumentProvider>(lowercasedDocumentName);
        if (documentProvider is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/plain;charset=utf-8";
            await context.Response.WriteAsync(
                $"No Arazzo document with the name '{lowercasedDocumentName}' was found.");
            return;
        }

        var isYaml = UseYaml(pattern);

        // Build and serialize the document into a buffer *before* committing any response headers,
        // so a failure surfaces as an accurate error status instead of a 200 with a broken body.
        string serialized;
        try
        {
            var document = await documentProvider.GetArazzoDocumentAsync(context.RequestAborted);
            serialized = isYaml ? ArazzoYamlWriter.Write(document) : ArazzoJsonWriter.Write(document);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            GetLogger(context).LogError(ex, "Failed to generate Arazzo document '{DocumentName}'.",
                SanitizeLog(lowercasedDocumentName));
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                $"Failed to generate the Arazzo document '{lowercasedDocumentName}'.");
            return;
        }

        if (string.IsNullOrWhiteSpace(serialized))
        {
            GetLogger(context).LogError("Arazzo document '{DocumentName}' serialized to an empty document.",
                SanitizeLog(lowercasedDocumentName));
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                $"The Arazzo document '{lowercasedDocumentName}' serialized to an empty document.");
            return;
        }

        var etag = ComputeETag(serialized);
        context.Response.Headers.ETag = etag;

        if (IsNotModified(context.Request, etag))
        {
            context.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        var payload = Encoding.UTF8.GetBytes(serialized);
        context.Response.ContentType = isYaml
            ? "application/vnd.oai.workflows+yaml;charset=utf-8"
            : "application/vnd.oai.workflows+json;charset=utf-8";
        context.Response.ContentLength = payload.Length;

        await context.Response.StartAsync();
        if (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        await context.Response.Body.WriteAsync(payload, context.RequestAborted);
    }

    private static string ComputeETag(string content)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return $"\"{Convert.ToHexStringLower(hashBytes)}\"";
    }

    /// <summary>
    /// Implements RFC 9110 §13.1.2 If-None-Match: a request matches (and so gets 304) if it carries "*", or
    /// any entity-tag in a comma-separated list matches under *weak* comparison — opaque-tags compared
    /// verbatim, ignoring the <c>W/</c> prefix on either side.
    /// </summary>
    private static bool IsNotModified(HttpRequest request, string etag)
    {
        var ifNoneMatch = request.GetTypedHeaders().IfNoneMatch;
        if (ifNoneMatch is not { Count: > 0 } || !EntityTagHeaderValue.TryParse(etag, out var responseTag))
        {
            return false;
        }

        foreach (var candidate in ifNoneMatch)
        {
            if (candidate.Tag.Equals(EntityTagHeaderValue.Any.Tag, StringComparison.Ordinal) ||
                candidate.Tag.Equals(responseTag.Tag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool UseYaml(string pattern) =>
        pattern.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
        pattern.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeLog(string? value) => value?.Replace("\r", "").Replace("\n", "") ?? string.Empty;

    private static ILogger GetLogger(HttpContext context) =>
        context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Bielu.AspNetCore.Arazzo")
        ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    /// <summary>Writes an RFC 7807 problem response. Safe to call only before the response has started, guaranteed here because serialization is fully buffered before any header is committed.</summary>
    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string detail)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json;charset=utf-8";

        var payload = JsonSerializer.SerializeToUtf8Bytes(new ArazzoProblemDetails(
            "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            "An error occurred while producing the Arazzo document.",
            statusCode,
            detail
        ), ArazzoJsonSchemaContext.Default.ArazzoProblemDetails);

        context.Response.ContentLength = payload.Length;
        await context.Response.Body.WriteAsync(payload, context.RequestAborted);
    }
}
