// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Bielu.AspNetCore.AsyncApi.Services;
using Microsoft.Extensions.Options;

namespace Bielu.AspNetCore.AsyncApi.Versioning;

internal sealed class ConfigureVersioningOptions(IApiVersionDescriptionProvider provider, Action<AsyncApiOptions, ApiVersionDescription>? configure)
    : IConfigureNamedOptions<AsyncApiOptions>
{
    public void Configure(string? name, AsyncApiOptions options)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var description = provider.ApiVersionDescriptions.FirstOrDefault(d => d.GroupName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (description != null)
        {
            options.DocumentName = description.GroupName;
            options.WithInfo(info =>
            {
                info.Version = description.ApiVersion.ToString();
                if (description.IsDeprecated)
                {
                    info.Description = (info.Description ?? "") + " (deprecated)";
                }
            });
            options.ShouldInclude = (apiDesc) => apiDesc.GroupName == description.GroupName;
            configure?.Invoke(options, description);
        }
    }

    public void Configure(AsyncApiOptions options) => Configure(Options.DefaultName, options);
}
