// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Data;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Services;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.OrderTracking.Hubs;
using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Extensions;
using ByteBard.AsyncAPI.Bindings.Kafka;
using ByteBard.AsyncAPI.Bindings.WebSockets;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Register custom metrics and tracing for this service
builder.AddServiceMetrics(OrderMetrics.MeterName);
builder.AddServiceTracing(DiagnosticsNames.OrderService);


// Register Aspire PostgreSQL with Entity Framework Core (connection managed by Aspire)
builder.AddNpgsqlDbContext<OrderDbContext>("ordersdb");

// Register Aspire Valkey/Redis cache (connection managed by Aspire)
builder.AddRedisClient("valkey");

builder.Services.AddSignalR();

// Register service layer and metrics
builder.Services.AddSingleton<OrderMetrics>();
builder.Services.AddScoped<IOrderService, Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Services.OrderService>();

builder.Services.AddAsyncApi(options =>
{
    options.AddServer("kafka", "kafka:9092", "kafka", pathName: null, server =>
    {
        server.Description = "Apache Kafka broker for order events";
    });

    options.AddServer("websocket", "localhost:5180", "ws", pathName: null, server =>
    {
        server.Description = "WebSocket server for real-time order tracking via SignalR";
    });

    options.WithDefaultContentType("application/json")
        .WithInfo("Order Service", "1.0.0")
        .WithDescription(
            "Order Service API — manages orders and publishes order lifecycle events via Kafka. " +
            "Provides real-time order tracking updates via WebSocket (SignalR). " +
            "Data is persisted to PostgreSQL (EF Core) and cached in Valkey.")
        .WithLicense("Apache 2.0", "https://www.apache.org/licenses/LICENSE-2.0");

    options.AddChannelBinding("kafka",
        new KafkaChannelBinding());

    options.AddChannelBinding("ws",
        new WebSocketsChannelBinding());
});

builder.Services.AddControllers();

var app = builder.Build();

// Apply EF Core migrations / ensure database is created
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapDefaultEndpoints();
app.UseRouting();

app.MapHub<OrderTrackingHub>("/hubs/order-tracking");

app.MapAsyncApi();
// Render the generated AsyncAPI document with Scalar (served at /scalar)
app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("v1", "Order Service", "/asyncapi/v1.json");
});
app.MapControllers();

app.Run();
