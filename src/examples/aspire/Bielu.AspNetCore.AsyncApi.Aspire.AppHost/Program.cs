// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure services

// Kafka message broker (official Aspire hosting package)
var kafka = builder.AddKafka("kafka");

// PostgreSQL database (official Aspire hosting package)
var postgres = builder.AddPostgres("postgres");
var ordersDb = postgres.AddDatabase("ordersdb");
var inventoryDb = postgres.AddDatabase("inventorydb");

// Valkey cache (official Aspire hosting package)
var valkey = builder.AddValkey("valkey");

// Apache Ozone object storage (custom container - no official Aspire hosting package)
var ozone = builder.AddContainer("ozone", "apache/ozone", "1.4.1")
    .WithHttpEndpoint(targetPort: 9878, name: "ozone-manager")
    .WithHttpEndpoint(targetPort: 9876, name: "ozone-scm")
    .WithArgs("ozone", "runAll");

// Microservices

var orderService = builder.AddProject<Projects.Bielu_AspNetCore_AsyncApi_Aspire_OrderService>("orderservice")
    .WithReference(kafka)
    .WithReference(ordersDb)
    .WithReference(valkey)
    .WaitFor(kafka)
    .WaitFor(ordersDb)
    .WaitFor(valkey);

var inventoryService = builder.AddProject<Projects.Bielu_AspNetCore_AsyncApi_Aspire_InventoryService>("inventoryservice")
    .WithReference(kafka)
    .WithReference(inventoryDb)
    .WaitFor(kafka)
    .WaitFor(inventoryDb);

var notificationService = builder.AddProject<Projects.Bielu_AspNetCore_AsyncApi_Aspire_NotificationService>("notificationservice")
    .WithReference(kafka)
    .WaitFor(kafka)
    .WaitFor(orderService)
    .WaitFor(inventoryService);

// API Gateway with YARP reverse proxy and merged AsyncAPI documentation
var apiGateway = builder.AddProject<Projects.Bielu_AspNetCore_AsyncApi_Aspire_ApiGateway>("apigateway")
    .WithReference(orderService)
    .WithReference(inventoryService)
    .WithReference(notificationService)
    .WaitFor(orderService)
    .WaitFor(inventoryService)
    .WaitFor(notificationService)
    .WithExternalHttpEndpoints();

builder.Build().Run();
