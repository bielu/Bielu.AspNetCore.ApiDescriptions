// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Merger.Extensions;
using Bielu.AspNetCore.AsyncApi.UI;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configure YARP reverse proxy with strongly-typed in-memory configuration.
var routes = new[]
{
    new RouteConfig
    {
        RouteId = "orders-route",
        ClusterId = "orderservice",
        Match = new RouteMatch { Path = "/api/orders/{**catch-all}" }
    },
    new RouteConfig
    {
        RouteId = "orders-ws-route",
        ClusterId = "orderservice",
        Match = new RouteMatch { Path = "/hubs/order-tracking/{**catch-all}" }
    },
    new RouteConfig
    {
        RouteId = "inventory-route",
        ClusterId = "inventoryservice",
        Match = new RouteMatch { Path = "/api/inventory/{**catch-all}" }
    },
    new RouteConfig
    {
        RouteId = "notifications-order-ws-route",
        ClusterId = "notificationservice",
        Match = new RouteMatch { Path = "/hubs/order-notifications/{**catch-all}" }
    },
    new RouteConfig
    {
        RouteId = "notifications-inventory-ws-route",
        ClusterId = "notificationservice",
        Match = new RouteMatch { Path = "/hubs/inventory-notifications/{**catch-all}" }
    }
};

var clusters = new[]
{
    new ClusterConfig
    {
        ClusterId = "orderservice",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            { "destination1", new DestinationConfig { Address = "http://orderservice" } }
        }
    },
    new ClusterConfig
    {
        ClusterId = "inventoryservice",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            { "destination1", new DestinationConfig { Address = "http://inventoryservice" } }
        }
    },
    new ClusterConfig
    {
        ClusterId = "notificationservice",
        Destinations = new Dictionary<string, DestinationConfig>
        {
            { "destination1", new DestinationConfig { Address = "http://notificationservice" } }
        }
    }
};

builder.Services.AddReverseProxy()
    .LoadFromMemory(routes, clusters);

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

// Enable WebSocket support for SignalR proxying
app.UseWebSockets();

// Map YARP reverse proxy routes
app.MapReverseProxy();

// Map the merged AsyncAPI document endpoint and UI
app.MapMergedAsyncApi("/asyncapi/merged.json");
app.MapAsyncApiUi("/asyncapi");

app.Run();
