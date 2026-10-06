// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Claims;
using Bielu.AspNetCore.AsyncApi.Extensions;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

namespace Bielu.AspNetCore.AsyncApi.Services;

/// <summary>
/// Defines a set of methods for generating AsyncApi definitions for endpoints.
/// </summary>
[RequiresUnreferencedCode(
    "AsyncApiGenerator performs reflection to generate AsyncApi descriptors. This cannot be statically analyzed.")]
[RequiresDynamicCode(
    "AsyncApiGenerator performs reflection to generate AsyncApi descriptors. This cannot be statically analyzed.")]
internal sealed class AsyncApiGenerator
{
    private readonly IHostEnvironment? _environment;
    private readonly IServiceProviderIsService? _serviceProviderIsService;

    /// <summary>
    /// Creates an <see cref="AsyncApiGenerator" /> instance given an <see cref="IHostEnvironment" />
    /// and an <see cref="IServiceProviderIsService" /> instance.
    /// </summary>
    /// <param name="environment">The host environment.</param>
    /// <param name="serviceProviderIsService">The service to determine if the type is available from the <see cref="IServiceProvider"/>.</param>
    internal AsyncApiGenerator(
        IHostEnvironment? environment,
        IServiceProviderIsService? serviceProviderIsService)
    {
        _environment = environment;
        _serviceProviderIsService = serviceProviderIsService;
    }

    /// <summary>
    /// Generates an <see cref="AsyncApiOperation"/> for a given <see cref="Endpoint" />.
    /// </summary>
    /// <param name="methodInfo">The <see cref="MethodInfo"/> associated with the route handler of the endpoint.</param>
    /// <param name="metadata">The endpoint <see cref="EndpointMetadataCollection"/>.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <returns>An <see cref="AsyncApiOperation"/> annotation derived from the given inputs.</returns>
    internal AsyncApiOperation? GetAsyncApiOperation(
        MethodInfo methodInfo,
        EndpointMetadataCollection metadata,
        RoutePattern pattern)
    {
        if (metadata.GetMetadata<IHttpMethodMetadata>() is { } httpMethodMetadata &&
            httpMethodMetadata.HttpMethods.SingleOrDefault() is { } method &&
            metadata.GetMetadata<IExcludeFromDescriptionMetadata>() is null or { ExcludeFromDescription: false })
        {
            return GetOperation(method, methodInfo, metadata, pattern);
        }

        return null;
    }

    private AsyncApiOperation GetOperation(string httpMethod, MethodInfo methodInfo,
        EndpointMetadataCollection metadata, RoutePattern pattern)
    {
        var disableInferredBody = ShouldDisableInferredBody(httpMethod);
        return new AsyncApiOperation
        {

            Summary = metadata.GetMetadata<IEndpointSummaryMetadata>()?.Summary,
            Description = metadata.GetMetadata<IEndpointDescriptionMetadata>()?.Description,
            Messages = GetAsyncApiMessages(methodInfo, metadata, pattern, disableInferredBody),
            Reply = GetAsyncApiResponses(methodInfo, metadata)
        };

        static bool ShouldDisableInferredBody(string method)
        {
            // GET, DELETE, HEAD, CONNECT, TRACE, and OPTIONS normally do not contain bodies
            return method.Equals(HttpMethods.Get, StringComparison.Ordinal) ||
                   method.Equals(HttpMethods.Delete, StringComparison.Ordinal) ||
                   method.Equals(HttpMethods.Head, StringComparison.Ordinal) ||
                   method.Equals(HttpMethods.Options, StringComparison.Ordinal) ||
                   method.Equals(HttpMethods.Trace, StringComparison.Ordinal) ||
                   method.Equals(HttpMethods.Connect, StringComparison.Ordinal);
        }
    }

    private static AsyncApiOperationReply GetAsyncApiResponses(MethodInfo method, EndpointMetadataCollection metadata)
    {
        var responses = new AsyncApiOperationReply();
        var responseType = method.ReturnType;
        // if (CoercedAwaitableInfo.IsTypeAwaitable(responseType, out var coercedAwaitableInfo))
        // {
        //     responseType = coercedAwaitableInfo.AwaitableInfo.ResultType;
        // }

        if (typeof(IResult).IsAssignableFrom(responseType))
        {
            responseType = typeof(void);
        }

        var errorMetadata = metadata.GetMetadata<ProducesErrorResponseTypeAttribute>();
        var defaultErrorType = errorMetadata?.Type;

        var responseProviderMetadata = metadata.GetOrderedMetadata<IApiResponseMetadataProvider>();
        var producesResponseMetadata = metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();

        var eligibileAnnotations = new Dictionary<int, (Type?, MediaTypeCollection)>();

        foreach (var responseMetadata in producesResponseMetadata)
        {
            var statusCode = responseMetadata.StatusCode;

            var discoveredTypeAnnotation = responseMetadata.Type;
            var discoveredContentTypeAnnotation = new MediaTypeCollection();

            if (discoveredTypeAnnotation == typeof(void))
            {
                if (responseType != null &&
                    (statusCode == StatusCodes.Status200OK || statusCode == StatusCodes.Status201Created))
                {
                    discoveredTypeAnnotation = responseType;
                }
            }

            foreach (var contentType in responseMetadata.ContentTypes)
            {
                discoveredContentTypeAnnotation.Add(contentType);
            }

            discoveredTypeAnnotation = discoveredTypeAnnotation == null || discoveredTypeAnnotation == typeof(void)
                ? responseType
                : discoveredTypeAnnotation;

            if (discoveredTypeAnnotation is not null)
            {
                GenerateDefaultContent(discoveredContentTypeAnnotation, discoveredTypeAnnotation);
                eligibileAnnotations[statusCode] = (discoveredTypeAnnotation, discoveredContentTypeAnnotation);
            }
        }

        foreach (var providerMetadata in responseProviderMetadata)
        {
            var statusCode = providerMetadata.StatusCode;

            var discoveredTypeAnnotation = providerMetadata.Type;
            var discoveredContentTypeAnnotation = new MediaTypeCollection();

            if (discoveredTypeAnnotation == typeof(void))
            {
                if (responseType != null &&
                    (statusCode == StatusCodes.Status200OK || statusCode == StatusCodes.Status201Created))
                {
                    // ProducesResponseTypeAttribute's constructor defaults to setting "Type" to void when no value is specified.
                    // In this event, use the action's return type for 200 or 201 status codes. This lets you decorate an action with a
                    // [ProducesResponseType(201)] instead of [ProducesResponseType(typeof(Person), 201] when typeof(Person) can be inferred
                    // from the return type.
                    discoveredTypeAnnotation = responseType;
                }
                else if (statusCode >= 400 && statusCode < 500)
                {
                    // Determine whether or not the type was provided by the user. If so, favor it over the default
                    // error type for 4xx client errors if no response type is specified.
                    discoveredTypeAnnotation =
                        defaultErrorType is not null ? defaultErrorType : discoveredTypeAnnotation;
                }
                else if (providerMetadata is IApiDefaultResponseMetadataProvider)
                {
                    discoveredTypeAnnotation = defaultErrorType;
                }
            }

            providerMetadata.SetContentTypes(discoveredContentTypeAnnotation);

            discoveredTypeAnnotation = discoveredTypeAnnotation == null || discoveredTypeAnnotation == typeof(void)
                ? responseType
                : discoveredTypeAnnotation;

            GenerateDefaultContent(discoveredContentTypeAnnotation, discoveredTypeAnnotation);
            eligibileAnnotations[statusCode] = (discoveredTypeAnnotation, discoveredContentTypeAnnotation);
        }

        if (responseType != null && eligibileAnnotations.Count == 0)
        {
            GenerateDefaultResponses(eligibileAnnotations, responseType!);
        }

        // foreach (var annotation in eligibileAnnotations)
        // {
        //     var statusCode = annotation.Key;
        //
        //     // TODO: Use the discarded response Type for schema generation
        //     var (_, contentTypes) = annotation.Value;
        //     var responseContent = new Dictionary<string, AsyncApiMediaType>();
        //
        //     foreach (var contentType in contentTypes)
        //     {
        //         responseContent[contentType] = new contenttype();
        //     }
        //
        //     responses[statusCode.ToString(CultureInfo.InvariantCulture)] = new AsyncApiResponse
        //     {
        //         Content = responseContent, Description = GetResponseDescription(statusCode)
        //     };
        // }

        return responses;
    }

    private static string GetResponseDescription(int statusCode)
        => ReasonPhrases.GetReasonPhrase(statusCode);

    private static void GenerateDefaultContent(MediaTypeCollection discoveredContentTypeAnnotation,
        Type? discoveredTypeAnnotation)
    {
        if (discoveredContentTypeAnnotation.Count == 0)
        {
            if (discoveredTypeAnnotation == typeof(void) || discoveredTypeAnnotation == null)
            {
                return;
            }

            if (discoveredTypeAnnotation == typeof(string))
            {
                discoveredContentTypeAnnotation.Add("text/plain");
            }
            else
            {
                discoveredContentTypeAnnotation.Add("application/json");
            }
        }
    }

    private static void GenerateDefaultResponses(Dictionary<int, (Type?, MediaTypeCollection)> eligibleAnnotations,
        Type responseType)
    {
        if (responseType == typeof(void))
        {
            eligibleAnnotations.Add(StatusCodes.Status200OK, (responseType, new MediaTypeCollection()));
        }
        else if (responseType == typeof(string))
        {
            eligibleAnnotations.Add(StatusCodes.Status200OK,
                (responseType, new MediaTypeCollection() { "text/plain" }));
        }
        else
        {
            eligibleAnnotations.Add(StatusCodes.Status200OK,
                (responseType, new MediaTypeCollection() { "application/json" }));
        }
    }

    private IList<AsyncApiMessageReference> GetAsyncApiMessages(MethodInfo methodInfo, EndpointMetadataCollection metadata,
        RoutePattern pattern, bool disableInferredBody)
    {
        //todo: implement
        return [];
    }

    private HashSet<AsyncApiTagReference> GetOperationTags(MethodInfo methodInfo, EndpointMetadataCollection metadata)
    {
        var metadataList = metadata.GetOrderedMetadata<ITagsMetadata>();
        var document = new AsyncApiDocument();

        if (metadataList.Count > 0)
        {
            var tags = new HashSet<AsyncApiTagReference>();

            foreach (var metadataItem in metadataList)
            {
                foreach (var tag in metadataItem.Tags)
                {
                    document.Components.Tags.Add(tag, new AsyncApiTag { Name = tag });
                    tags.Add(new AsyncApiTagReference(tag));
                }
            }

            return tags;
        }

        string controllerName;
        //todo  && !TypeHelper.IsCompilerGeneratedType(methodInfo.DeclaringType)
        if (methodInfo.DeclaringType is not null)
        {
            controllerName = methodInfo.DeclaringType.Name;
        }
        else
        {
            // If the declaring type is null or compiler-generated (e.g. lambdas),
            // group the methods under the application name.
            controllerName = _environment?.ApplicationName ?? string.Empty;
        }
        document.Components.Tags.Add(controllerName, new AsyncApiTag { Name = controllerName });
        return [new AsyncApiTagReference(controllerName)];

    }

    private List<AsyncApiParameter> GetAsyncApiParameters(MethodInfo methodInfo, RoutePattern pattern,
        bool disableInferredBody)
    {
        var parameters = methodInfo.GetParameters();
        var AsyncApiParameters = new List<AsyncApiParameter>();

        foreach (var parameter in parameters)
        {
            if (parameter.Name is null)
            {
                throw new InvalidOperationException(
                    $"Encountered a parameter of type '{parameter.ParameterType}' without a name. Parameters must have a name.");
            }

            var (_, parameterLocation, attributeName) =
                GetAsyncApiParameterLocation(parameter, pattern, disableInferredBody);

            // if the parameter doesn't have a valid location
            // then we should ignore it
            if (parameterLocation is null)
            {
                continue;
            }

            var nullabilityContext = new NullabilityInfoContext();
            var nullability = nullabilityContext.Create(parameter);
            var isOptional = parameter.HasDefaultValue || nullability.ReadState != NullabilityState.NotNull;
            var name = attributeName ?? (pattern.GetParameter(parameter.Name) is { } routeParameter
                ? routeParameter.Name
                : parameter.Name);
            var AsyncApiParameter = new AsyncApiParameter()
            {
                Description = name,
                Location = parameterLocation.Value.ToString()
            };
            AsyncApiParameters.Add(AsyncApiParameter);
        }

        return AsyncApiParameters;
    }

    private (bool isBodyOrForm, ParameterLocation? locatedIn, string? name) GetAsyncApiParameterLocation(
        ParameterInfo parameter, RoutePattern pattern, bool disableInferredBody)
    {
        var attributes = parameter.GetCustomAttributes();

        if (attributes.OfType<IFromQueryMetadata>().FirstOrDefault() is { } queryAttribute)
        {
            return (false, ParameterLocation.Query, queryAttribute.Name);
        }

        if (attributes.OfType<IFromHeaderMetadata>().FirstOrDefault() is { } headerAttribute)
        {
            return (false, ParameterLocation.Header, headerAttribute.Name);
        }

        if (attributes.OfType<IFromBodyMetadata>().FirstOrDefault() is { } fromBodyAttribute)
        {
            return (true, null, null);
        }

        if (attributes.OfType<IFromFormMetadata>().FirstOrDefault() is { } fromFormAttribute)
        {
            return (true, null, null);
        }

        if (parameter.CustomAttributes.Any(a =>
                typeof(IFromServiceMetadata).IsAssignableFrom(a.AttributeType) ||
                typeof(FromKeyedServicesAttribute).IsAssignableFrom(a.AttributeType)) ||
            parameter.ParameterType == typeof(HttpContext) ||
            parameter.ParameterType == typeof(HttpRequest) ||
            parameter.ParameterType == typeof(HttpResponse) ||
            parameter.ParameterType == typeof(ClaimsPrincipal) ||
            parameter.ParameterType == typeof(CancellationToken) ||
            parameter.HasBindAsyncMethod() ||
            _serviceProviderIsService?.IsService(parameter.ParameterType) == true)
        {
            return (false, null, null);
        }

        if (parameter.ParameterType == typeof(string) || parameter.ParameterType.HasTryParseMethod())
        {
            return (false, ParameterLocation.Query, null);
        }

        if (parameter.ParameterType == typeof(IFormFile) ||
            parameter.ParameterType == typeof(IFormFileCollection) ||
            parameter.ParameterType.IsJsonPatchDocument())
        {
            return (true, null, null);
        }

        if (disableInferredBody && (
                parameter.ParameterType == typeof(string[]) ||
                parameter.ParameterType == typeof(StringValues) ||
                (parameter.ParameterType.IsArray &&
                 parameter.ParameterType.GetElementType()!.HasTryParseMethod())))
        {
            return (false, ParameterLocation.Query, null);
        }


        return (true, null, null);
    }
}
