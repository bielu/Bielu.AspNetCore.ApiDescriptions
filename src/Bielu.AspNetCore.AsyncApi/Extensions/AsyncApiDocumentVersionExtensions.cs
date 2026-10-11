// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Extensions;

/// <summary>
/// Extension methods that tell which AsyncAPI specification version an <see cref="AsyncApiDocument"/> declares.
/// </summary>
public static class AsyncApiDocumentVersionExtensions
{
    /// <summary>
    /// Whether <paramref name="document"/> declares <paramref name="version"/> in its <c>asyncapi</c> field.
    /// Only the major version is compared, so <c>2.6.0</c> matches <see cref="AsyncApiVersion.AsyncApi2_0"/>
    /// and <c>3.1.0</c> matches <see cref="AsyncApiVersion.AsyncApi3_0"/>. A document that declares no
    /// version matches none.
    /// </summary>
    /// <param name="document">The document to inspect.</param>
    /// <param name="version">The specification version to test for.</param>
    /// <returns><see langword="true"/> when the document declares that version; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not a known version.</exception>
    /// <example>
    /// Transformers use it to skip objects that only exist in one version, such as an operation's reply:
    /// <code>
    /// options.AddDocumentTransformer((document, context, cancellationToken) =>
    /// {
    ///     if (document.IsAsyncApiVersion(AsyncApiVersion.AsyncApi3_0))
    ///     {
    ///         // add 3.x-only content
    ///     }
    ///
    ///     return Task.CompletedTask;
    /// });
    /// </code>
    /// </example>
    public static bool IsAsyncApiVersion(this AsyncApiDocument document, AsyncApiVersion version)
    {
        ArgumentNullException.ThrowIfNull(document);

        var prefix = version switch
        {
            AsyncApiVersion.AsyncApi2_0 => "2.",
            AsyncApiVersion.AsyncApi3_0 => "3.",
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, "Unknown AsyncAPI version."),
        };

        return document.Asyncapi?.StartsWith(prefix, StringComparison.Ordinal) == true;
    }
}
