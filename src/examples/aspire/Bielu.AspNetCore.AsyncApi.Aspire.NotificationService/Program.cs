// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications;
using Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Hubs;
using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Extensions;
using ByteBard.AsyncAPI.Bindings.WebSockets;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Register custom metrics and tracing for this service
builder.AddServiceMetrics(NotificationMetrics.MeterName);
builder.AddServiceTracing(DiagnosticsNames.NotificationService);


builder.Services.AddSignalR();

// Register metrics and background service that consumes Kafka events and pushes to SignalR
builder.Services.AddSingleton<NotificationMetrics>();
builder.Services.AddHostedService<KafkaNotificationWorker>();

builder.Services.AddAsyncApi(options =>
{
    options.AddServer("websocket", "localhost:5183", "ws", pathName: null, server =>
    {
        server.Description = "WebSocket server for real-time notifications via SignalR";
    });

    options.WithDefaultContentType("application/json")
        .WithInfo("Notification Service", "1.0.0")
        .WithDescription(
            "Notification Service — delivers real-time notifications to clients via WebSocket (SignalR). " +
            "Consumes order and inventory events from Kafka and pushes updates to connected clients.")
        .WithLicense("Apache 2.0", "https://www.apache.org/licenses/LICENSE-2.0");

    options.AddChannelBinding("ws",
        new WebSocketsChannelBinding());
});

builder.Services.AddControllers();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseRouting();

app.MapHub<OrderNotificationHub>("/hubs/order-notifications");
app.MapHub<InventoryNotificationHub>("/hubs/inventory-notifications");

app.MapAsyncApi();
// Render the generated AsyncAPI document with Scalar (served at /scalar)
app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("v1", "Notification Service", "/asyncapi/v1.json");
});
app.MapControllers();

app.Run();
