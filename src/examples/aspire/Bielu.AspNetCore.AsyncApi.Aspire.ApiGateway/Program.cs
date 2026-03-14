// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Merger.Extensions;
using Bielu.AspNetCore.AsyncApi.UI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configure YARP reverse proxy from configuration
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Configure AsyncAPI document merging from all downstream microservices.
// The merger fetches AsyncAPI docs from each microservice and combines them into a single document.
builder.Services.AddAsyncApiMerge(options =>
{
    // Enumerate all services from configuration and register their AsyncAPI documents.
    // Aspire populates "services:{name}:http:0" with the resolved URLs at runtime.
    var servicesSection = builder.Configuration.GetSection("services");
    foreach (var serviceSection in servicesSection.GetChildren())
    {
        var serviceName = serviceSection.Key;
        var serviceUrl = serviceSection.GetSection("http:0").Value;

        if (string.IsNullOrEmpty(serviceUrl))
        {
            serviceUrl = $"http://{serviceName}";
        }

        options.AddSource(serviceUrl.TrimEnd('/') + "/asyncapi/v1.json", serviceName);
    }

    options.Info = new ByteBard.AsyncAPI.Models.AsyncApiInfo
    {
        Title = "Mini Shop",
        Version = "1.0.0",
        Description = "Merged AsyncAPI documentation from all Mini Shop microservices, served via YARP API Gateway."
    };
});

var app = builder.Build();

app.MapDefaultEndpoints();

// Map YARP reverse proxy routes
app.MapReverseProxy();

// Map the merged AsyncAPI document endpoint and UI
app.MapMergedAsyncApi("/asyncapi/merged.json");
app.MapAsyncApiUi("/asyncapi");

app.Run();
