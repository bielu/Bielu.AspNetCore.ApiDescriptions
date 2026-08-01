// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Services;

/// <summary>
/// Describes a single ASP.NET Core authentication scheme discovered from the application's
/// <see cref="Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider"/> so it can be
/// mapped to an <see cref="AsyncApiSecurityScheme"/>.
/// </summary>
public sealed class DetectedAuthenticationScheme
{
    /// <summary>
    /// Creates a new <see cref="DetectedAuthenticationScheme"/>.
    /// </summary>
    /// <param name="name">The scheme name (the key used to declare the scheme in AsyncAPI components).</param>
    /// <param name="displayName">The human-friendly display name, if any.</param>
    /// <param name="handlerType">The CLR type of the authentication handler backing the scheme.</param>
    public DetectedAuthenticationScheme(string name, string? displayName, Type handlerType)
    {
        Name = name;
        DisplayName = displayName;
        HandlerType = handlerType;
    }

    /// <summary>The scheme name, e.g. <c>Bearer</c>, <c>Cookies</c> or a custom scheme name.</summary>
    public string Name { get; }

    /// <summary>The human-friendly display name registered for the scheme, if any.</summary>
    public string? DisplayName { get; }

    /// <summary>The CLR type of the <c>IAuthenticationHandler</c> backing this scheme.</summary>
    public Type HandlerType { get; }

    /// <summary>
    /// Returns <see langword="true"/> when the backing handler's simple type name matches
    /// <paramref name="handlerTypeName"/>. Matching by name avoids taking a hard dependency on the
    /// authentication packages (JwtBearer, OpenIdConnect, Negotiate) that live outside the shared framework.
    /// </summary>
    public bool HandlerIs(string handlerTypeName)
        => string.Equals(HandlerType.Name, handlerTypeName, StringComparison.Ordinal);
}

/// <summary>
/// Default handler-type to <see cref="AsyncApiSecurityScheme"/> mappings used by
/// <see cref="AsyncApiOptions.DetectAuthenticationSchemes"/>.
/// </summary>
public static class AuthenticationSchemeDefaults
{
    /// <summary>
    /// Maps the built-in ASP.NET Core authentication handlers to the closest AsyncAPI security scheme.
    /// Returns <see langword="null"/> for handlers whose shape cannot be inferred without additional
    /// configuration (custom handlers, OAuth2/OpenID Connect flows) — supply those via a custom
    /// <see cref="AuthenticationDetectionOptions.Map"/> delegate.
    /// </summary>
    public static AsyncApiSecurityScheme? DefaultMap(DetectedAuthenticationScheme scheme)
    {
        return scheme.HandlerType.Name switch
        {
            // JWT bearer token carried in the HTTP Authorization header.
            "JwtBearerHandler" => AsyncApiSecurityScheme.Http(
                "bearer",
                "JWT",
                $"JWT bearer authentication (scheme '{scheme.Name}')."),

            // Cookie-based session. The real cookie name is only known from CookieAuthenticationOptions;
            // the ASP.NET Core default is used here and can be overridden via a custom Map delegate.
            "CookieAuthenticationHandler" => AsyncApiSecurityScheme.HttpApiKey(
                ParameterLocation.Cookie,
                ".AspNetCore.Cookies",
                $"Cookie authentication (scheme '{scheme.Name}')."),

            // Windows / Kerberos / NTLM via the Negotiate HTTP authentication scheme.
            "NegotiateHandler" => AsyncApiSecurityScheme.Http(
                "negotiate",
                description: $"Windows (Negotiate) authentication (scheme '{scheme.Name}')."),

            // OAuth2 / OpenID Connect need authorization/token/discovery URLs that are not reliably
            // discoverable here — return null so the caller can supply them explicitly.
            _ => null,
        };
    }
}

/// <summary>
/// Options controlling how ASP.NET Core authentication schemes are projected into an AsyncAPI document
/// by <see cref="AsyncApiOptions.DetectAuthenticationSchemes"/>.
/// </summary>
public sealed class AuthenticationDetectionOptions
{
    /// <summary>
    /// Maps a discovered authentication scheme to an <see cref="AsyncApiSecurityScheme"/>, or
    /// <see langword="null"/> to skip it. Defaults to <see cref="AuthenticationSchemeDefaults.DefaultMap"/>.
    /// Override this to describe custom handlers (for example a custom API-key handler whose location
    /// and parameter name cannot be inferred) or to remap for a specific protocol.
    /// </summary>
    public Func<DetectedAuthenticationScheme, AsyncApiSecurityScheme?> Map { get; set; }
        = AuthenticationSchemeDefaults.DefaultMap;

    /// <summary>
    /// When <see langword="true"/> (the default), each mapped scheme is referenced from the document's
    /// servers so consumers (such as the Scalar authentication panel) treat it as required.
    /// </summary>
    public bool AttachToServers { get; set; } = true;

    /// <summary>
    /// When <see langword="true"/>, security requirements are attached at the operation level to the
    /// operations of channels whose declaring type or method is annotated with <c>[Authorize]</c>
    /// (respecting <c>[AllowAnonymous]</c>). This yields per-channel precision for documents that mix
    /// public and secured channels. A bare <c>[Authorize]</c> attaches every detected scheme; an
    /// <c>[Authorize(AuthenticationSchemes = "...")]</c> attaches only the named schemes that were
    /// detected. Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Building that map means <strong>scanning the loaded assemblies</strong> for <c>[AsyncApi]</c>
    /// types and reading their members' attributes — the authorization data lives on the declaring
    /// types, not in the generated document. That is reflection over types no static analysis can
    /// predict, so under trimming or Native AOT those types may be gone and channels can silently
    /// come back unsecured. Leave this off in a trimmed or AOT application unless the annotated types
    /// are rooted explicitly.
    /// </remarks>
    public bool AttachToAuthorizedOperations { get; set; }

    /// <summary>
    /// When <see langword="true"/>, a mapped scheme replaces an existing security scheme with the same
    /// name. Defaults to <see langword="false"/> so hand-authored schemes always win over detection.
    /// </summary>
    public bool OverwriteExisting { get; set; }

    /// <summary>
    /// Restricts <see cref="AttachToServers"/> to the given sanitized server keys. When empty (the
    /// default) detected schemes are attached to every server in the document.
    /// </summary>
    public ISet<string> ServerKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
