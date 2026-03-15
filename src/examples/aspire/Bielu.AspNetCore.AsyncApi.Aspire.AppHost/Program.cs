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

// Apache Ozone object storage (custom multi-container setup — no official Aspire hosting package)
// Based on https://github.com/apache/ozone-docker/blob/latest/docker-compose.yaml

const string ozoneImage = "apache/ozone";
const string ozoneTag = "2.1.0";

var ozoneScm = WithOzoneConfig(builder.AddContainer("ozone-scm", ozoneImage, ozoneTag))
    .WithHttpEndpoint(targetPort: 9876, name: "ozone-scm-http")
    .WithEnvironment("ENSURE_SCM_INITIALIZED", "/data/metadata/scm/current/VERSION")
    .WithArgs("ozone", "scm");

var ozoneOm = WithOzoneConfig(builder.AddContainer("ozone-om", ozoneImage, ozoneTag))
    .WithHttpEndpoint(targetPort: 9874, name: "ozone-om-http")
    .WithEnvironment("ENSURE_OM_INITIALIZED", "/data/metadata/om/current/VERSION")
    .WithEnvironment("WAITFOR", "ozone-scm:9876")
    .WithArgs("ozone", "om")
    .WaitFor(ozoneScm);

var ozoneDatanode = WithOzoneConfig(builder.AddContainer("ozone-datanode", ozoneImage, ozoneTag))
    .WithArgs("ozone", "datanode");

var ozoneS3g = WithOzoneConfig(builder.AddContainer("ozone-s3g", ozoneImage, ozoneTag))
    .WithHttpEndpoint(targetPort: 9878, name: "ozone-s3")
    .WithArgs("ozone", "s3g")
    .WaitFor(ozoneOm);

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
    .WithReference(valkey)
    .WithReference(ozoneS3g.GetEndpoint("ozone-s3"))
    .WaitFor(kafka)
    .WaitFor(inventoryDb)
    .WaitFor(valkey)
    .WaitFor(ozoneS3g);

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

// Applies the common Ozone environment variables shared by all Ozone containers.
// See: https://github.com/apache/ozone-docker/blob/latest/docker-compose.yaml
static IResourceBuilder<ContainerResource> WithOzoneConfig(IResourceBuilder<ContainerResource> container)
{
    return container
        .WithEnvironment("OZONE-SITE.XML_hdds.datanode.dir", "/data/hdds")
        .WithEnvironment("OZONE-SITE.XML_ozone.metadata.dirs", "/data/metadata")
        .WithEnvironment("OZONE-SITE.XML_ozone.om.address", "ozone-om")
        .WithEnvironment("OZONE-SITE.XML_ozone.om.http-address", "ozone-om:9874")
        .WithEnvironment("OZONE-SITE.XML_ozone.scm.names", "ozone-scm")
        .WithEnvironment("OZONE-SITE.XML_ozone.scm.client.address", "ozone-scm")
        .WithEnvironment("OZONE-SITE.XML_ozone.scm.block.client.address", "ozone-scm")
        .WithEnvironment("OZONE-SITE.XML_ozone.scm.datanode.id.dir", "/data/metadata")
        .WithEnvironment("OZONE-SITE.XML_ozone.replication", "1");
}
