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
    // These URLs use Aspire service discovery names.
    // At runtime, Aspire resolves "orderservice" and "inventoryservice" to actual addresses.
    var orderServiceUrl = builder.Configuration["services:orderservice:http:0"]
        ?? "http://orderservice/asyncapi/v1.json";
    var inventoryServiceUrl = builder.Configuration["services:inventoryservice:http:0"]
        ?? "http://inventoryservice/asyncapi/v1.json";

    options.AddSource(orderServiceUrl.TrimEnd('/') + "/asyncapi/v1.json", "orders");
    options.AddSource(inventoryServiceUrl.TrimEnd('/') + "/asyncapi/v1.json", "inventory");

    options.Info = new ByteBard.AsyncAPI.Models.AsyncApiInfo
    {
        Title = "Unified AsyncAPI Gateway",
        Version = "1.0.0",
        Description = "Merged AsyncAPI documentation from all microservices, served via YARP API Gateway."
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
