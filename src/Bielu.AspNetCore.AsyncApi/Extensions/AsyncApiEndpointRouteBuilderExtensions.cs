// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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
    public static IEndpointConventionBuilder MapAsyncApi(this IEndpointRouteBuilder endpoints, [StringSyntax("Route")] string pattern = AsyncApiGeneratorConstants.DefaultAsyncApiRoute)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptionsMonitor<AsyncApiOptions>>();
        // Store the pattern so the middleware can use it
        options.CurrentValue.DocumentRoutePattern = pattern;
        return endpoints.MapGet(pattern, async (HttpContext context, string documentName = AsyncApiGeneratorConstants.DefaultDocumentName) =>
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
                var documentService = context.RequestServices.GetKeyedService<AsyncApiDocumentService>(lowercasedDocumentName);
                if (documentService is null)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    context.Response.ContentType = "text/plain;charset=utf-8";
                    await context.Response.WriteAsync($"No AsyncApi document with the name '{lowercasedDocumentName}' was found.");
                }
                else
                {
                    var document = await documentService.GetAsyncApiDocumentAsync(context.RequestServices, context.Request, context.RequestAborted);
                    var documentOptions = options.Get(lowercasedDocumentName);

                    var isYaml = UseYaml(pattern);

                    // Serialize the document
                    string serialized;
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

                    string contentType = isYaml ? "text/plain+yaml;charset=utf-8" : "application/json;charset=utf-8";
                    context.Response.ContentType = contentType;

                    await context.Response.StartAsync();
                    if (context.RequestAborted.IsCancellationRequested)
                    {
                        return;
                    }

                    await context.Response.WriteAsync(serialized, context.RequestAborted);
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
}
