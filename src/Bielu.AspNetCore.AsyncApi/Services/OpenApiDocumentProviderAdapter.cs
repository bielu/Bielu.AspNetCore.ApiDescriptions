// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.ApiDescriptions;

/// <summary>
/// Reaches <c>Microsoft.AspNetCore.OpenApi</c>'s build-time document provider by reflection, so that
/// <see cref="AsyncApiDocumentProvider"/> can serve OpenAPI documents alongside AsyncAPI ones.
/// </summary>
/// <remarks>
/// Both libraries declare an internal <c>Microsoft.Extensions.ApiDescriptions.IDocumentProvider</c>, and
/// <c>dotnet getdocument</c> resolves only the first one it finds across the loaded assemblies. Whichever
/// one it picks has to answer for both libraries, which is why the provider types are reached by name here.
/// The adapter instantiates <c>OpenApiDocumentProvider</c> directly rather than resolving the OpenAPI
/// <c>IDocumentProvider</c> service, because that service may itself be forwarded to
/// <see cref="AsyncApiDocumentProvider"/> (see <see cref="OpenApiDocumentProviderProxy"/>).
/// </remarks>
internal sealed class OpenApiDocumentProviderAdapter
{
    internal const string DocumentProviderInterfaceName = "Microsoft.Extensions.ApiDescriptions.IDocumentProvider";
    private const string OpenApiAssemblyName = "Microsoft.AspNetCore.OpenApi";
    private const string DocumentProviderTypeName = "Microsoft.Extensions.ApiDescriptions.OpenApiDocumentProvider";

    private readonly object _provider;
    private readonly MethodInfo _getDocumentNames;
    private readonly MethodInfo _generate;
    private readonly MethodInfo? _generateWithVersion;

    private OpenApiDocumentProviderAdapter(object provider, MethodInfo getDocumentNames, MethodInfo generate, MethodInfo? generateWithVersion)
    {
        _provider = provider;
        _getDocumentNames = getDocumentNames;
        _generate = generate;
        _generateWithVersion = generateWithVersion;
    }

    /// <summary>
    /// Creates an adapter over <c>Microsoft.AspNetCore.OpenApi</c>'s provider, or returns <see langword="null"/>
    /// when that assembly isn't available or its provider doesn't have the expected shape.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only reached by build-time document generation, which runs the app under the JIT.")]
    [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "Only reached by build-time document generation, which runs the app under the JIT.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Only reached by build-time document generation, which runs the app under the JIT.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Only reached by build-time document generation, which runs the app under the JIT.")]
    public static OpenApiDocumentProviderAdapter? TryCreate(IServiceProvider serviceProvider)
    {
        var providerType = Type.GetType($"{DocumentProviderTypeName}, {OpenApiAssemblyName}", throwOnError: false);
        if (providerType is null)
        {
            return null;
        }

        var getDocumentNames = providerType.GetMethod(nameof(IDocumentProvider.GetDocumentNames), Type.EmptyTypes);
        var generate = providerType.GetMethod(nameof(IDocumentProvider.GenerateAsync), [typeof(string), typeof(TextWriter)]);
        if (getDocumentNames is null || generate is null)
        {
            return null;
        }

        var generateWithVersion = providerType.GetMethods()
            .FirstOrDefault(method => method.Name == nameof(IDocumentProvider.GenerateAsync)
                && method.GetParameters() is [{ ParameterType: var name }, { ParameterType: var writer }, { ParameterType.IsEnum: true }]
                && name == typeof(string)
                && writer == typeof(TextWriter));

        var provider = ActivatorUtilities.CreateInstance(serviceProvider, providerType);
        return new OpenApiDocumentProviderAdapter(provider, getDocumentNames, generate, generateWithVersion);
    }

    /// <summary>
    /// The OpenAPI document names registered through <c>AddOpenApi</c>.
    /// </summary>
    public IEnumerable<string> GetDocumentNames() =>
        (IEnumerable<string>?)_getDocumentNames.Invoke(_provider, null) ?? [];

    /// <summary>
    /// Writes the named OpenAPI document. <paramref name="openApiSpecVersion"/>, when given, is the
    /// <c>Microsoft.OpenApi.OpenApiSpecVersion</c> value <c>dotnet getdocument</c> passes through.
    /// </summary>
    public Task GenerateAsync(string documentName, TextWriter writer, object? openApiSpecVersion = null)
    {
        var task = openApiSpecVersion is not null && _generateWithVersion is not null
            && _generateWithVersion.GetParameters()[2].ParameterType.IsInstanceOfType(openApiSpecVersion)
                ? _generateWithVersion.Invoke(_provider, [documentName, writer, openApiSpecVersion])
                : _generate.Invoke(_provider, [documentName, writer]);
        return (Task)task!;
    }
}
