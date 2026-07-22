// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Asp.Versioning.ApiExplorer;
using Bielu.AspNetCore.AsyncApi.Services;

namespace Bielu.AspNetCore.AsyncApi.Versioning;

internal sealed class ApiVersionDocumentNamesProvider(IApiVersionDescriptionProvider provider) : IAsyncApiDocumentNamesProvider
{
    public IEnumerable<string> GetDocumentNames() => provider.ApiVersionDescriptions.Select(d => d.GroupName);
}
