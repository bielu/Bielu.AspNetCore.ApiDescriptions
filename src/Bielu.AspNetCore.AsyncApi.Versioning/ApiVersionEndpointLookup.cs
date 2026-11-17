// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.AsyncApi.Versioning;

/// <summary>
/// The <see cref="ApiVersionMetadata"/> Asp.Versioning attached to the app's endpoints, by the controller action
/// or minimal API handler behind them. It reflects versioning conventions as well as attributes.
/// </summary>
/// <remarks>
/// The endpoints are read once, the first time a document needs them; by then the app has been built and its
/// routes mapped, at runtime and during build-time generation alike.
/// </remarks>
internal sealed class ApiVersionEndpointLookup
{
    private readonly Lazy<(ILookup<MethodInfo, ApiVersionMetadata> Methods, ILookup<Type, ApiVersionMetadata> Types)> _endpoints;

    public ApiVersionEndpointLookup(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _endpoints = new(() => Read(services.GetService<EndpointDataSource>()));
    }

    public IReadOnlyList<ApiVersionMetadata> ForMethod(MethodInfo method) => [.. _endpoints.Value.Methods[method]];

    public IReadOnlyList<ApiVersionMetadata> ForType(Type type) => [.. _endpoints.Value.Types[type]];

    private static (ILookup<MethodInfo, ApiVersionMetadata>, ILookup<Type, ApiVersionMetadata>) Read(EndpointDataSource? dataSource)
    {
        var entries = new List<(MethodInfo Method, Type? Controller, ApiVersionMetadata Metadata)>();
        foreach (var endpoint in dataSource?.Endpoints ?? [])
        {
            if (endpoint.Metadata.GetMetadata<ApiVersionMetadata>() is not { } metadata || !IsVersioned(metadata))
            {
                continue;
            }

            if (endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is { } action)
            {
                entries.Add((action.MethodInfo, action.ControllerTypeInfo.AsType(), metadata));
            }
            else if (endpoint.Metadata.GetMetadata<MethodInfo>() is { } handler)
            {
                entries.Add((handler, null, metadata));
            }
        }

        return (
            entries.ToLookup(entry => entry.Method, entry => entry.Metadata),
            entries.Where(entry => entry.Controller is not null).ToLookup(entry => entry.Controller!, entry => entry.Metadata));
    }

    /// <summary>Endpoints outside versioned APIs carry empty metadata, which says nothing about versions.</summary>
    private static bool IsVersioned(ApiVersionMetadata metadata)
        => metadata.IsApiVersionNeutral
           || metadata.Map(ApiVersionMapping.Explicit | ApiVersionMapping.Implicit).DeclaredApiVersions.Count > 0;
}
