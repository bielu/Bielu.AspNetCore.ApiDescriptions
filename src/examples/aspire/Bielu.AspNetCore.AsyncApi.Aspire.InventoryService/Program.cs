// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Data;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Services;
using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.UI;
using ByteBard.AsyncAPI.Bindings.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Register custom metrics and tracing for this service
builder.AddServiceMetrics(InventoryMetrics.MeterName);
builder.AddServiceTracing(DiagnosticsNames.InventoryService);

// Register Aspire Confluent Kafka producer and consumer (connection managed by Aspire)
builder.AddKafkaProducer<string, string>("kafka");
builder.AddKafkaConsumer<string, string>("kafka");

// Register Aspire PostgreSQL with Entity Framework Core (connection managed by Aspire)
builder.AddNpgsqlDbContext<InventoryDbContext>("inventorydb");

// Register Aspire Valkey/Redis cache (connection managed by Aspire)
builder.AddRedisClient("valkey");

// Register service layer and metrics
builder.Services.AddSingleton<InventoryMetrics>();
builder.Services.AddScoped<IInventoryManagementService, InventoryManagementService>();

// Decorate with Valkey caching (Scrutor decorator pattern)
builder.Services.Decorate<IInventoryManagementService, CachedInventoryManagementService>();

builder.Services.AddAsyncApi(options =>
{
    options.AddServer("kafka", "kafka:9092", "kafka", server =>
    {
        server.Description = "Apache Kafka broker for inventory events";
    });

    options.WithDefaultContentType("application/json")
        .WithInfo("Inventory Service", "1.0.0")
        .WithDescription(
            "Inventory Service API — manages product inventory and reacts to order events via Kafka. " +
            "Data is persisted to PostgreSQL (EF Core). Uses Apache Ozone for document storage.")
        .WithLicense("Apache 2.0", "https://www.apache.org/licenses/LICENSE-2.0");

    options.AddChannelBinding("kafkaInventoryChannel",
        new KafkaChannelBinding());
});

builder.Services.AddControllers();

var app = builder.Build();

// Apply EF Core migrations / ensure database is created with seed data
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapDefaultEndpoints();
app.UseRouting();

app.MapAsyncApi();
app.MapAsyncApiUi();
app.MapControllers();

app.Run();
