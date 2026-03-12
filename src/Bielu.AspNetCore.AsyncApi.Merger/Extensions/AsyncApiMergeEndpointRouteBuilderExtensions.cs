// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Bielu.AspNetCore.AsyncApi.Merger.Merge;
using Bielu.AspNetCore.AsyncApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.AsyncApi.Merger.Extensions;

/// <summary>
/// Extension methods for mapping merged AsyncAPI document endpoints.
/// </summary>
public static class AsyncApiMergeEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps an endpoint that serves a merged AsyncAPI document from multiple sources.
    /// The merged document is cached and remote sources are periodically checked for changes.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern for the merged document endpoint.</param>
    /// <returns>An endpoint convention builder for further customization.</returns>
    public static IEndpointConventionBuilder MapMergedAsyncApi(this IEndpointRouteBuilder endpoints, [StringSyntax("Route")] string pattern = "/asyncapi/merged.json")
    {
        return endpoints.MapGet(pattern, async (HttpContext context) =>
        {
            var mergeService = context.RequestServices.GetRequiredService<CachedAsyncApiMergeService>();

            var document = await mergeService.GetMergedDocumentAsync(context.RequestAborted);

            var isYaml = pattern.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                         pattern.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);

            var contentType = isYaml ? "text/plain+yaml;charset=utf-8" : "application/json;charset=utf-8";
            context.Response.ContentType = contentType;

            await context.Response.StartAsync();
            if (context.RequestAborted.IsCancellationRequested)
            {
                return;
            }

            string serialized;
            if (isYaml)
            {
                serialized = AsyncApiSerializationHelper.SerializeV3ToYaml(document);
            }
            else
            {
                serialized = AsyncApiSerializationHelper.SerializeV3ToJson(document);
            }

            await context.Response.WriteAsync(serialized, context.RequestAborted);
        }).ExcludeFromDescription();
    }
}
