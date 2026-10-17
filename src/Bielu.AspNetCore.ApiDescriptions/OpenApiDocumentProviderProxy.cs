// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Microsoft.Extensions.ApiDescriptions;

/// <summary>
/// Implements <c>Microsoft.AspNetCore.OpenApi</c>'s internal <c>IDocumentProvider</c> by forwarding to
/// <see cref="BuildTimeDocumentProvider"/>, which serves the OpenAPI documents and those of every <see cref="Bielu.AspNetCore.ApiDescriptions.IBuildTimeDocumentSource"/>.
/// </summary>
/// <remarks>
/// <c>dotnet getdocument</c> resolves whichever <c>IDocumentProvider</c> type it finds first across the loaded
/// assemblies. When that is the OpenAPI one, this proxy is what it gets back, so the other documents are
/// still listed and written. The interface is internal to another assembly, so it can only be implemented at
/// runtime, which is what <see cref="DispatchProxy"/> does.
/// </remarks>
internal class OpenApiDocumentProviderProxy : DispatchProxy
{
    private BuildTimeDocumentProvider _target = null!;

    /// <summary>
    /// Creates a proxy implementing <paramref name="interfaceType"/> that forwards to <paramref name="target"/>.
    /// </summary>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only resolved by build-time document generation, which runs the app under the JIT.")]
    public static object Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type interfaceType,
        BuildTimeDocumentProvider target)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        ArgumentNullException.ThrowIfNull(target);

        var proxy = DispatchProxy.Create(interfaceType, typeof(OpenApiDocumentProviderProxy));
        ((OpenApiDocumentProviderProxy)proxy)._target = target;
        return proxy;
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => (targetMethod?.Name, args) switch
    {
        (nameof(IDocumentProvider.GetDocumentNames), _) => _target.GetDocumentNames(),
        (nameof(IDocumentProvider.GenerateAsync), [string documentName, TextWriter writer]) =>
            _target.GenerateAsync(documentName, writer),
        (nameof(IDocumentProvider.GenerateAsync), [string documentName, TextWriter writer, var openApiSpecVersion]) =>
            _target.GenerateWithOpenApiVersionAsync(documentName, writer, openApiSpecVersion),
        _ => throw new NotSupportedException($"'{targetMethod?.Name}' is not supported by {nameof(OpenApiDocumentProviderProxy)}."),
    };
}
