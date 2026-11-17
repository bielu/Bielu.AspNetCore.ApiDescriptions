using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Bielu.Arazzo.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.Arazzo.Transformers;

/// <summary>An <see cref="IArazzoDocumentTransformer"/> backed by a delegate.</summary>
internal sealed class DelegateArazzoDocumentTransformer(
    Func<ArazzoDocument, ArazzoDocumentTransformerContext, CancellationToken, Task> transformer) : IArazzoDocumentTransformer
{
    public Task TransformAsync(ArazzoDocument document, ArazzoDocumentTransformerContext context, CancellationToken cancellationToken)
        => transformer(document, context, cancellationToken);
}

/// <summary>
/// An <see cref="IArazzoDocumentTransformer"/> registered by type: an instance is created from the application
/// services for each run, and disposed afterwards.
/// </summary>
internal sealed class TypeBasedArazzoDocumentTransformer : IArazzoDocumentTransformer
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private readonly Type _transformerType;
    private readonly ObjectFactory _transformerFactory;

    internal TypeBasedArazzoDocumentTransformer([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type transformerType)
    {
        _transformerType = transformerType;
        _transformerFactory = ActivatorUtilities.CreateFactory(_transformerType, []);
    }

    public async Task TransformAsync(ArazzoDocument document, ArazzoDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var transformer = _transformerFactory.Invoke(context.ApplicationServices, []) as IArazzoDocumentTransformer;
        Debug.Assert(transformer is not null, $"The type {_transformerType} does not implement {nameof(IArazzoDocumentTransformer)}.");
        try
        {
            await transformer.TransformAsync(document, context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (transformer is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (transformer is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
