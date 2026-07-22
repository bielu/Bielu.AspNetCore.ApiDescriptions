// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Services;

/// <summary>
/// Provides a way to discover additional AsyncApi document names dynamically.
/// </summary>
public interface IAsyncApiDocumentNamesProvider
{
    /// <summary>
    /// Gets the names of the documents provided by this provider.
    /// </summary>
    /// <returns>A collection of document names.</returns>
    IEnumerable<string> GetDocumentNames();
}
