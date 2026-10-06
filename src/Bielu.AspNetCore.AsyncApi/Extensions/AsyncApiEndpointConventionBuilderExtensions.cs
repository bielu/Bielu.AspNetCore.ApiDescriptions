// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bielu.AspNetCore.AsyncApi.Extensions;

/// <summary>
/// Extension methods for annotating AsyncApi descriptions on an <see cref="Endpoint" />.
/// </summary>
public static class AsyncApiEndpointConventionBuilderExtensions
{
    private const string TrimWarningMessage = "Calls Bielu.AspNetCore.AsyncApi.Services.AsyncApiGenerator.GetAsyncApiOperation(MethodInfo, EndpointMetadataCollection, RoutePattern) which uses dynamic analysis. Use IServiceCollection.AddAsyncApi() to generate AsyncApi metadata at startup for all endpoints,";



    [RequiresDynamicCode(TrimWarningMessage)]
    [RequiresUnreferencedCode(TrimWarningMessage)]
    private static void AddAndConfigureOperationForEndpoint(EndpointBuilder endpointBuilder, Func<AsyncApiOperation, AsyncApiOperation>? configure = null)
    {
        foreach (var item in endpointBuilder.Metadata)
        {
            if (item is AsyncApiOperation existingOperation)
            {
                if (configure is not null)
                {
                    var configuredOperation = configure(existingOperation);

                    if (!ReferenceEquals(configuredOperation, existingOperation))
                    {
                        endpointBuilder.Metadata.Remove(existingOperation);

                        // The only way configureOperation could be null here is if configureOperation violated it's signature and returned null.
                        // We could throw or something, removing the previous metadata seems fine.
                        if (configuredOperation is not null)
                        {
                            endpointBuilder.Metadata.Add(configuredOperation);
                        }
                    }
                }

                return;
            }
        }

        // We cannot generate an AsyncApiOperation without routeEndpointBuilder.RoutePattern.
        if (endpointBuilder is not RouteEndpointBuilder routeEndpointBuilder)
        {
            return;
        }

        var pattern = routeEndpointBuilder.RoutePattern;
        var metadata = new EndpointMetadataCollection(routeEndpointBuilder.Metadata);
        var methodInfo = metadata.OfType<MethodInfo>().SingleOrDefault();

        if (methodInfo is null)
        {
            return;
        }

        var applicationServices = routeEndpointBuilder.ApplicationServices;
        var hostEnvironment = applicationServices.GetService<IHostEnvironment>();
        var serviceProviderIsService = applicationServices.GetService<IServiceProviderIsService>();
        var generator = new AsyncApiGenerator(hostEnvironment, serviceProviderIsService);
        var newOperation = generator.GetAsyncApiOperation(methodInfo, metadata, pattern);

        if (newOperation is not null)
        {
            if (configure is not null)
            {
                newOperation = configure(newOperation);
            }

            if (newOperation is not null)
            {
                routeEndpointBuilder.Metadata.Add(newOperation);
            }
        }
    }

    /// <summary>
    /// Adds an AsyncApi operation transformer to the <see cref="EndpointBuilder.Metadata" /> associated
    /// with the current endpoint.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <param name="transformer">The <see cref="Func{AsyncApiOperation, AsyncApiOperationTransformerContext, CancellationToken, Task}"/> that modifies the operation in the <see cref="AsyncApiDocument"/>.</param>
    /// <returns>A <see cref="IEndpointConventionBuilder"/> that can be used to further customize the endpoint.</returns>
    public static TBuilder AddAsyncApiOperationTransformer<TBuilder>(this TBuilder builder, Func<AsyncApiOperation, AsyncApiOperationTransformerContext, CancellationToken, Task> transformer) where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new DelegateAsyncApiOperationTransformer(transformer));
        return builder;
    }
}
