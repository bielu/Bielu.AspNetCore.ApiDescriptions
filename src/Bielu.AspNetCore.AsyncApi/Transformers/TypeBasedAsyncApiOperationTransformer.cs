// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.AsyncApi.Transformers;

internal sealed class TypeBasedAsyncApiOperationTransformer : IAsyncApiOperationTransformer
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private readonly Type _transformerType;
    private readonly ObjectFactory _transformerFactory;

    internal TypeBasedAsyncApiOperationTransformer([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type transformerType)
    {
        _transformerType = transformerType;
        _transformerFactory = ActivatorUtilities.CreateFactory(_transformerType, []);
    }

    internal IAsyncApiOperationTransformer InitializeTransformer(IServiceProvider serviceProvider)
    {
        var transformer = _transformerFactory.Invoke(serviceProvider, []) as IAsyncApiOperationTransformer;
        Debug.Assert(transformer != null, $"The type {_transformerType} does not implement {nameof(IAsyncApiOperationTransformer)}.");
        return transformer;
    }

    /// <remarks>
    /// Throw because the activate instance is invoked by the <see cref="AsyncApiDocumentService" />.
    /// </remarks>
    public Task TransformAsync(AsyncApiOperation operation, AsyncApiOperationTransformerContext context, CancellationToken cancellationToken)
        => throw new InvalidOperationException("This method should not be called. Only activated instances of this transformer should be used.");
}
