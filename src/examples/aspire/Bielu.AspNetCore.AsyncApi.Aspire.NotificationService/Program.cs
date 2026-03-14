// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications;
using Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Hubs;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.UI;
using ByteBard.AsyncAPI.Bindings.WebSockets;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Register custom metrics and tracing for this service
builder.AddServiceMetrics(NotificationMetrics.MeterName);
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("MiniShop.NotificationService"));

// Register Aspire Confluent Kafka consumer (connection managed by Aspire)
builder.AddKafkaConsumer<string, string>("kafka");

builder.Services.AddSignalR();

// Register metrics and background service that consumes Kafka events and pushes to SignalR
builder.Services.AddSingleton<NotificationMetrics>();
builder.Services.AddHostedService<KafkaNotificationWorker>();

builder.Services.AddAsyncApi(options =>
{
    options.AddServer("websocket", "localhost:5183", "ws", server =>
    {
        server.Description = "WebSocket server for real-time notifications via SignalR";
    });

    options.WithDefaultContentType("application/json")
        .WithInfo("Notification Service", "1.0.0")
        .WithDescription(
            "Notification Service — delivers real-time notifications to clients via WebSocket (SignalR). " +
            "Consumes order and inventory events from Kafka and pushes updates to connected clients.")
        .WithLicense("Apache 2.0", "https://www.apache.org/licenses/LICENSE-2.0");

    options.AddChannelBinding("wsNotificationChannel",
        new WebSocketsChannelBinding());
});

builder.Services.AddControllers();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseRouting();

app.MapHub<OrderNotificationHub>("/hubs/order-notifications");
app.MapHub<InventoryNotificationHub>("/hubs/inventory-notifications");

app.MapAsyncApi();
app.MapAsyncApiUi();
app.MapControllers();

app.Run();
