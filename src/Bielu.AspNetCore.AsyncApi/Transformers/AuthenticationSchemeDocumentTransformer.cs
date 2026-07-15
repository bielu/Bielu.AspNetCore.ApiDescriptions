// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Helpers;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.AsyncApi.Transformers;

/// <summary>
/// Populates <c>components.securitySchemes</c> (and optionally the servers' or authorized operations'
/// security requirements) from the ASP.NET Core authentication schemes registered on the application,
/// so authentication does not have to be described by hand. Schemes the mapper cannot infer are skipped,
/// leaving the user free to declare additional schemes explicitly. Hand-authored schemes are never
/// overwritten unless <see cref="AuthenticationDetectionOptions.OverwriteExisting"/> is set.
/// </summary>
internal sealed class AuthenticationSchemeDocumentTransformer(AuthenticationDetectionOptions options)
    : IAsyncApiDocumentTransformer
{
    public async Task TransformAsync(
        AsyncApiDocument document,
        AsyncApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemeProvider = context.ApplicationServices.GetService<IAuthenticationSchemeProvider>();
        if (schemeProvider is null)
        {
            // Authentication is not registered in this application; nothing to detect.
            return;
        }

        var schemes = await schemeProvider.GetAllSchemesAsync();

        document.Components ??= new AsyncApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, AsyncApiSecurityScheme>();

        // Keys that should be referenced: everything we mapped, plus schemes the user already declared
        // under the same name (so an override still gets wired up).
        var referencedKeys = new List<string>();

        foreach (var scheme in schemes)
        {
            var detected = new DetectedAuthenticationScheme(scheme.Name, scheme.DisplayName, scheme.HandlerType);
            var mapped = options.Map(detected);
            if (mapped is null)
            {
                continue;
            }

            var key = scheme.Name;
            if (!document.Components.SecuritySchemes.ContainsKey(key) || options.OverwriteExisting)
            {
                document.Components.SecuritySchemes[key] = mapped;
            }

            if (!referencedKeys.Contains(key))
            {
                referencedKeys.Add(key);
            }
        }

        if (referencedKeys.Count == 0)
        {
            return;
        }

        var isV2 = document.Asyncapi?.StartsWith("2.", StringComparison.Ordinal) == true;

        if (options.AttachToServers)
        {
            AttachToServers(document, referencedKeys, isV2);
        }

        if (options.AttachToAuthorizedOperations)
        {
            AttachToAuthorizedOperations(document, context.DocumentName, referencedKeys, isV2);
        }
    }

    private void AttachToServers(AsyncApiDocument document, IReadOnlyList<string> referencedKeys, bool isV2)
    {
        if (document.Servers is null)
        {
            return;
        }

        foreach (var (serverKey, server) in document.Servers)
        {
            if (options.ServerKeys.Count > 0 && !options.ServerKeys.Contains(serverKey))
            {
                continue;
            }

            server.Security ??= new List<AsyncApiSecurityScheme>();
            AddReferences(server.Security, referencedKeys, isV2);
        }
    }

    private static void AttachToAuthorizedOperations(
        AsyncApiDocument document,
        string documentName,
        IReadOnlyList<string> referencedKeys,
        bool isV2)
    {
        if (document.Operations is not { Count: > 0 })
        {
            return;
        }

        var authorizedChannels = BuildAuthorizedChannels(documentName);
        if (authorizedChannels.Count == 0)
        {
            return;
        }

        foreach (var (_, operation) in document.Operations)
        {
            var channelKey = ExtractChannelKey(operation.Channel);
            if (channelKey is null || !authorizedChannels.TryGetValue(channelKey, out var explicitSchemes))
            {
                continue;
            }

            // Empty explicit set means a bare [Authorize] — attach every detected scheme. Otherwise
            // attach only the named schemes that were actually detected (avoids dangling references).
            var keysToAttach = explicitSchemes.Count > 0
                ? referencedKeys.Where(explicitSchemes.Contains).ToList()
                : referencedKeys;

            if (keysToAttach.Count == 0)
            {
                continue;
            }

            operation.Security ??= new List<AsyncApiSecurityScheme>();
            AddReferences(operation.Security, keysToAttach, isV2);
        }
    }

    private static void AddReferences(IList<AsyncApiSecurityScheme> target, IReadOnlyList<string> keys, bool isV2)
    {
        foreach (var key in keys)
        {
            // V2 references the bare fragment id (#key); V3 uses the full JSON pointer.
            var reference = isV2 ? $"#{key}" : $"#/components/securitySchemes/{key}";

            var alreadyReferenced = target
                .OfType<AsyncApiSecuritySchemeReference>()
                .Any(existing => string.Equals(existing.Reference?.Reference, reference, StringComparison.Ordinal));

            if (!alreadyReferenced)
            {
                target.Add(new AsyncApiSecuritySchemeReference(reference));
            }
        }
    }

    /// <summary>
    /// Extracts the sanitized channel key from an operation's channel reference
    /// (e.g. <c>#/channels/secureChatHub</c> or the V2 form <c>#secureChatHub</c>).
    /// </summary>
    private static string? ExtractChannelKey(AsyncApiChannelReference? channel)
    {
        var reference = channel?.Reference?.Reference;
        if (string.IsNullOrEmpty(reference))
        {
            return null;
        }

        var lastSlash = reference.LastIndexOf('/');
        var key = lastSlash >= 0 ? reference[(lastSlash + 1)..] : reference.TrimStart('#');
        return key.Length == 0 ? null : key;
    }

    /// <summary>
    /// Scans the types that declare this document's channels for <c>[Authorize]</c> / <c>[AllowAnonymous]</c>
    /// and returns the sanitized channel keys that require authorization. The value is the set of explicitly
    /// named authentication schemes (from <c>[Authorize(AuthenticationSchemes = "...")]</c>); an empty set
    /// means a bare <c>[Authorize]</c> that applies to every detected scheme.
    /// </summary>
    private static Dictionary<string, HashSet<string>> BuildAuthorizedChannels(string documentName)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            Type?[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            foreach (var type in types)
            {
                if (type is null)
                {
                    continue;
                }

                var asyncApi = type.GetCustomAttribute<AsyncApiAttribute>(inherit: true);
                if (asyncApi is null)
                {
                    continue;
                }

                if (asyncApi.DocumentName is not null &&
                    !string.Equals(asyncApi.DocumentName, documentName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var typeAttributes = type.GetCustomAttributes(inherit: true);
                var typeAuthorize = typeAttributes.OfType<IAuthorizeData>().ToArray();
                var typeAllowAnonymous = typeAttributes.OfType<IAllowAnonymous>().Any();

                foreach (var channelAttr in type.GetCustomAttributes<ChannelAttribute>(inherit: true))
                {
                    ApplyAuthorization(result, channelAttr.Name, typeAuthorize, typeAllowAnonymous);
                }

                var methods = type.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

                foreach (var method in methods)
                {
                    var methodChannels = method.GetCustomAttributes<ChannelAttribute>(inherit: true).ToArray();
                    if (methodChannels.Length == 0)
                    {
                        continue;
                    }

                    var methodAttributes = method.GetCustomAttributes(inherit: true);
                    var methodAuthorizeOnly = methodAttributes.OfType<IAuthorizeData>().ToArray();
                    var methodAllowAnonymous = methodAttributes.OfType<IAllowAnonymous>().Any();

                    // The method inherits the type's [Authorize] unless it opts out via [AllowAnonymous].
                    var effectiveAuthorize = methodAuthorizeOnly.Concat(typeAuthorize).ToArray();
                    var effectiveAllowAnonymous = methodAllowAnonymous ||
                        (typeAllowAnonymous && methodAuthorizeOnly.Length == 0);

                    foreach (var channelAttr in methodChannels)
                    {
                        ApplyAuthorization(result, channelAttr.Name, effectiveAuthorize, effectiveAllowAnonymous);
                    }
                }
            }
        }

        return result;
    }

    private static void ApplyAuthorization(
        Dictionary<string, HashSet<string>> result,
        string channelName,
        IReadOnlyList<IAuthorizeData> authorizeData,
        bool allowAnonymous)
    {
        if (allowAnonymous || authorizeData.Count == 0)
        {
            return;
        }

        var key = AsyncApiNamingHelper.SanitizeKey(channelName);
        if (!result.TryGetValue(key, out var schemes))
        {
            result[key] = schemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var data in authorizeData)
        {
            if (string.IsNullOrWhiteSpace(data.AuthenticationSchemes))
            {
                continue;
            }

            foreach (var scheme in data.AuthenticationSchemes.Split(
                ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                schemes.Add(scheme);
            }
        }
    }
}
