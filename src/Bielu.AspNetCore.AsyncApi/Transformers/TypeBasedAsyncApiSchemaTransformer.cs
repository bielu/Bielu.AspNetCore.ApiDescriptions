// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Bielu.AspNetCore.AsyncApi.Services.Schemas;
using ByteBard.AsyncAPI.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.AsyncApi.Transformers;

internal sealed class TypeBasedAsyncApiSchemaTransformer : IAsyncApiSchemaTransformer
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private readonly Type _transformerType;
    private readonly ObjectFactory _transformerFactory;

    internal TypeBasedAsyncApiSchemaTransformer([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type transformerType)
    {
        _transformerType = transformerType;
        _transformerFactory = ActivatorUtilities.CreateFactory(_transformerType, []);
    }

    internal IAsyncApiSchemaTransformer InitializeTransformer(IServiceProvider serviceProvider)
    {
        var transformer = _transformerFactory.Invoke(serviceProvider, []) as IAsyncApiSchemaTransformer;
        Debug.Assert(transformer != null, $"The type {_transformerType} does not implement {nameof(IAsyncApiSchemaTransformer)}.");
        return transformer;
    }

    /// <remarks>
    /// Throw because the activate instance is invoked by the <see cref="AsyncApiJsonSchemaService" />.
    /// </remarks>
    public Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context, CancellationToken cancellationToken)
        => throw new InvalidOperationException("This method should not be called. Only activated instances of this transformer should be used.");
}
