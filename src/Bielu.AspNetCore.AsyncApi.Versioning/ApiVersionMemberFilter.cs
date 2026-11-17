// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Asp.Versioning;
using Bielu.AspNetCore.AsyncApi.Models.Metadata;

namespace Bielu.AspNetCore.AsyncApi.Versioning;

/// <summary>
/// Decides whether a declared AsyncAPI member belongs in the document of one API version.
/// </summary>
/// <remarks>
/// <para>
/// When the member is an endpoint (a controller action or a minimal API handler) that Asp.Versioning has
/// versioned, the endpoint's <see cref="ApiVersionMetadata"/> decides, so versions applied by conventions
/// count as well as attributes. A class-level declaration follows the endpoints of that class: it is in a
/// version when at least one of its actions is.
/// </para>
/// <para>
/// Otherwise the Asp.Versioning attributes decide, read through <see cref="IApiVersionProvider"/> and
/// <see cref="IApiVersionNeutral"/> so custom attributes work too. A method's own
/// <c>[ApiVersionNeutral]</c>, then <c>[MapToApiVersion]</c>, then <c>[ApiVersion]</c> win over its class's;
/// a method with none of them takes its class's versions. <c>[AdvertiseApiVersions]</c> declares versions
/// implemented elsewhere, so it is ignored. A member with no version information at all, and a
/// version-neutral one, is in every version's document.
/// </para>
/// </remarks>
internal sealed class ApiVersionMemberFilter(ApiVersion apiVersion, ApiVersionEndpointLookup endpoints)
{
    public bool Includes(AsyncApiTypeMetadata typeMetadata, AsyncApiMemberMetadata memberMetadata)
    {
        ArgumentNullException.ThrowIfNull(typeMetadata);
        ArgumentNullException.ThrowIfNull(memberMetadata);

        var endpointMetadata = memberMetadata.Member is Type type
            ? endpoints.ForType(type)
            : memberMetadata.Member is MethodInfo method ? endpoints.ForMethod(method) : [];
        if (endpointMetadata.Count > 0)
        {
            return endpointMetadata.Any(metadata => metadata.IsApiVersionNeutral || metadata.IsMappedTo(apiVersion));
        }

        var scope = memberMetadata.Member is Type
            ? ScopeOf(typeMetadata.Type)
            : ScopeOf(memberMetadata.Member) ?? ScopeOf(typeMetadata.Type);
        return scope is null || scope.IsNeutral || scope.Versions.Contains(apiVersion);
    }

    /// <summary>The versions <paramref name="member"/> itself declares, or <see langword="null"/> when it declares none.</summary>
    internal static VersionScope? ScopeOf(MemberInfo member)
    {
        var attributes = member.GetCustomAttributes(inherit: true);
        if (attributes.OfType<IApiVersionNeutral>().Any())
        {
            return VersionScope.Neutral;
        }

        var providers = attributes.OfType<IApiVersionProvider>()
            .Where(provider => !provider.Options.HasFlag(ApiVersionProviderOptions.Advertised))
            .ToList();
        var mapped = providers.Where(provider => provider.Options.HasFlag(ApiVersionProviderOptions.Mapped)).ToList();
        var declaring = mapped.Count > 0 ? mapped : providers;

        return declaring.Count == 0
            ? null
            : new VersionScope(false, declaring.SelectMany(provider => provider.Versions).ToHashSet());
    }

    internal sealed record VersionScope(bool IsNeutral, IReadOnlySet<ApiVersion> Versions)
    {
        public static VersionScope Neutral { get; } = new(true, new HashSet<ApiVersion>());
    }
}
