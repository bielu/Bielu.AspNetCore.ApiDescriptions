// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.UI;
using ByteBard.AsyncAPI.Bindings.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddAsyncApi(options =>
{
    options.AddServer("kafka", "kafka:9092", "kafka", server =>
    {
        server.Description = "Apache Kafka broker for order events";
    });

    options.WithDefaultContentType("application/json")
        .WithInfo("Order Service", "1.0.0")
        .WithDescription(
            "Order Service API — manages orders and publishes order lifecycle events via Kafka.")
        .WithLicense("Apache 2.0", "https://www.apache.org/licenses/LICENSE-2.0");

    options.AddChannelBinding("kafkaOrderChannel",
        new KafkaChannelBinding());
});

builder.Services.AddControllers();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseRouting();

app.MapAsyncApi();
app.MapAsyncApiUi();
app.MapControllers();

app.Run();
