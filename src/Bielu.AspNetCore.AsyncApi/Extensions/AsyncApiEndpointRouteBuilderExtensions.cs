// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Schemas;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Bielu.AspNetCore.AsyncApi.Extensions;

/// <summary>
/// AsyncApi-related methods for <see cref="IEndpointRouteBuilder"/>.
/// </summary>
public static class AsyncApiEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Register an endpoint onto the current application for resolving the AsyncApi document associated
    /// with the current application.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/>.</param>
    /// <param name="pattern">The route to register the endpoint on. Must include the 'documentName' route parameter.</param>
    /// <returns>An <see cref="IEndpointRouteBuilder"/> that can be used to further customize the endpoint.</returns>
    public static IEndpointConventionBuilder MapAsyncApi(this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern = AsyncApiGeneratorConstants.DefaultAsyncApiRoute)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>();
        // Store the pattern so the middleware can use it
        options.CurrentValue.DocumentRoutePattern = pattern;
        return endpoints.MapGet(pattern,
            async (HttpContext context, string documentName = AsyncApiGeneratorConstants.DefaultDocumentName) =>
            {
                // We need to retrieve the document name in a case-insensitive manner to support case-insensitive document name resolution.
                // The document service is registered with a key equal to the document name, but in lowercase.
                // The GetRequiredKeyedService() method is case-sensitive, which doesn't work well for AsyncApi document names here,
                // as the document name is also used as the route to retrieve the document, so we need to ensure this is lowercased to achieve consistency with ASP.NET Core routing.
                // The same goes for the document options below, which is also case-sensitive, and thus we need to pass in a case-insensitive document name.
                // See AsyncApiServiceCollectionExtensions.cs for more info.
                var lowercasedDocumentName = documentName.ToLowerInvariant();

                // It would be ideal to use the `HttpResponseStreamWriter` to
                // asynchronously write to the response stream here but Microsoft.AsyncApi
                // does not yet support async APIs on their writers.
                // See https://github.com/microsoft/AsyncApi.NET/issues/421 for more info.
                var documentService =
                    context.RequestServices.GetKeyedService<AsyncApiDocumentService>(lowercasedDocumentName);
                if (documentService is null)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    context.Response.ContentType = "text/plain;charset=utf-8";
                    await context.Response.WriteAsync(
                        $"No AsyncApi document with the name '{lowercasedDocumentName}' was found.");
                }
                else
                {
                    var isYaml = UseYaml(pattern);

                    // Build and serialize the document into a buffer *before* committing any response
                    // headers. If this throws, no headers are sent yet, so we can still return an
                    // accurate error status instead of a 200 with a broken body (see issue #31).
                    string serialized;
                    try
                    {
                        var document = await documentService.GetAsyncApiDocumentAsync(context.RequestServices,
                            context.Request, context.RequestAborted);
                        var documentOptions = options.Get(lowercasedDocumentName);

                        if (documentOptions.AsyncApiVersion == AsyncApiVersion.AsyncApi2_0)
                        {
                            serialized = isYaml
                                ? AsyncApiSerializationHelper.SerializeV2ToYaml(document)
                                : AsyncApiSerializationHelper.SerializeV2ToJson(document);
                        }
                        else
                        {
                            serialized = isYaml
                                ? AsyncApiSerializationHelper.SerializeV3ToYaml(document)
                                : AsyncApiSerializationHelper.SerializeV3ToJson(document);
                        }

                        serialized = await AsyncApiSerializedDocumentPipeline.ApplyAsync(
                            serialized,
                            documentOptions,
                            lowercasedDocumentName,
                            isYaml ? AsyncApiDocumentFormat.Yaml : AsyncApiDocumentFormat.Json,
                            context.RequestServices,
                            context.RequestAborted);
                    }
                    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                    {
                        // The client disconnected; headers were never committed, so just stop.
                        return;
                    }
                    catch (Exception ex)
                    {
                        GetLogger(context).LogError(ex, "Failed to generate AsyncApi document '{DocumentName}'.",
                            SanitizeLog(lowercasedDocumentName));
                        await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                            $"Failed to generate the AsyncApi document '{lowercasedDocumentName}'.");
                        return;
                    }

                    // Guard against an empty/whitespace document being served as a successful 200.
                    if (string.IsNullOrWhiteSpace(serialized))
                    {
                        GetLogger(context)
                            .LogError("AsyncApi document '{DocumentName}' serialized to an empty document.",
                                SanitizeLog(lowercasedDocumentName));
                        await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                            $"The AsyncApi document '{lowercasedDocumentName}' serialized to an empty document.");
                        return;
                    }

                    // Compute ETag from serialized content
                    var etag = ComputeETag(serialized);
                    context.Response.Headers.ETag = etag;

                    // Check If-None-Match for conditional request support
                    if (context.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var ifNoneMatch) &&
                        ifNoneMatch.ToString().Trim() == etag)
                    {
                        context.Response.StatusCode = StatusCodes.Status304NotModified;
                        return;
                    }

                    var payload = Encoding.UTF8.GetBytes(serialized);
                    context.Response.ContentType =
                        isYaml ? "text/plain+yaml;charset=utf-8" : "application/json;charset=utf-8";
                    // Set Content-Length so a truncated write surfaces as a protocol error rather than a
                    // silently short body.
                    context.Response.ContentLength = payload.Length;

                    await context.Response.StartAsync();
                    if (context.RequestAborted.IsCancellationRequested)
                    {
                        return;
                    }

                    await context.Response.Body.WriteAsync(payload, context.RequestAborted);
                }
            }).ExcludeFromDescription();
    }

    /// <summary>
    /// Computes a weak ETag from the serialized document content using a SHA256 hash.
    /// </summary>
    private static string ComputeETag(string content)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        var hashHex = Convert.ToHexStringLower(hashBytes);
        return $"\"{hashHex}\"";
    }

    private static bool UseYaml(string pattern) =>
        pattern.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
        pattern.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeLog(string? value)
    {
        if (value is null) return string.Empty;
        return value.Replace("\r", "").Replace("\n", "");
    }

    private static ILogger GetLogger(HttpContext context) =>
        context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Bielu.AspNetCore.AsyncApi")
        ?? NullLoggerInstance;

    private static readonly ILogger NullLoggerInstance =
        Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    /// <summary>
    /// Writes an RFC 7807 problem response. Safe to call only before the response has started, which
    /// is guaranteed here because serialization is fully buffered before any header is committed.
    /// </summary>
    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string detail)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json;charset=utf-8";

        var payload = JsonSerializer.SerializeToUtf8Bytes(new AsyncApiProblemDetails(
            "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            "An error occurred while producing the AsyncApi document.",
            statusCode,
            detail
        ), AsyncApiJsonSchemaContext.Default.AsyncApiProblemDetails);

        context.Response.ContentLength = payload.Length;
        await context.Response.Body.WriteAsync(payload, context.RequestAborted);
    }
}
