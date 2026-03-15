// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

// Workaround for librdkafka native library loading on platforms where the default
// P/Invoke search path does not find the library or its dependencies.
// On Linux the default librdkafka.so requires libsasl2.so.3 which may be absent;
// the centos8 variant statically links SASL and has no such dependency.
// On Windows/macOS the library may not be found via default search paths when
// running from the Aspire AppHost.
// Pre-loading ensures the library is available before Aspire.Hosting.Kafka registers
// its HealthChecks.Kafka health check (used by WaitFor).
// See: https://github.com/confluentinc/confluent-kafka-dotnet/issues/778
var librdkafkaHandle = PreloadLibrdkafka();
if (librdkafkaHandle != IntPtr.Zero)
{
    NativeLibrary.SetDllImportResolver(
        typeof(Confluent.Kafka.ProducerBuilder<string, string>).Assembly,
        (libraryName, _, _) => libraryName == "librdkafka" ? librdkafkaHandle : IntPtr.Zero);
}

var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure services

// Kafka message broker (official Aspire hosting package — provides the Apache Kafka broker
// that is a prerequisite for Aspire.Confluent.Kafka connectors used by the services).
// Uses confluentinc/confluent-local Docker image with KRaft mode (no ZooKeeper needed).
var kafka = builder.AddKafka("kafka")
    .WithKafkaUI()
    .WithDataVolume();

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
    .WithArgs("ozone", "datanode")
    .WaitFor(ozoneScm);

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
// The "OZONE-SITE.XML_" prefix is an Apache Ozone convention — the Docker entrypoint
// converts these environment variables into ozone-site.xml configuration entries.
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

// Pre-loads the correct librdkafka native library variant for the current platform.
// Returns an IntPtr handle on success, or IntPtr.Zero if no pre-loading is needed
// (e.g., the default P/Invoke search will find the library on its own).
// NOTE: Uses ProcessArchitecture (not OSArchitecture) because the native DLL must
// match the running process — e.g., x64 .NET on Windows ARM64 needs win-x64 binaries.
static IntPtr PreloadLibrdkafka()
{
    string baseDir = AppContext.BaseDirectory;

    foreach (var candidate in GetLibrdkafkaCandidates(baseDir))
    {
        if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
        {
            return handle;
        }
    }

    return IntPtr.Zero;
}

// Returns candidate paths in priority order for the current platform.
// librdkafka.redist 2.12.0 ships: linux-arm64, linux-x64, osx-arm64, osx-x64, win-x64, win-x86.
// Notably win-arm64 is NOT shipped — Windows ARM64 users must use the x64 .NET SDK
// (runs under emulation) until upstream adds win-arm64 support.
static IEnumerable<string> GetLibrdkafkaCandidates(string baseDir)
{
    var arch = RuntimeInformation.ProcessArchitecture;

    if (OperatingSystem.IsLinux())
    {
        var rid = arch == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        // centos8 variant has SASL statically linked (no libsasl2.so.3 dependency)
        yield return Path.Combine(baseDir, "runtimes", rid, "native", "centos8-librdkafka.so");
        yield return Path.Combine(baseDir, "runtimes", rid, "native", "librdkafka.so");
    }
    else if (OperatingSystem.IsWindows())
    {
        var rid = arch switch
        {
            Architecture.X86 => "win-x86",
            Architecture.Arm64 => "win-arm64",
            _ => "win-x64",
        };
        yield return Path.Combine(baseDir, "runtimes", rid, "native", "librdkafka.dll");
        // Fallback: on ARM64 try x64 (works when process runs under x64 emulation)
        if (rid != "win-x64")
            yield return Path.Combine(baseDir, "runtimes", "win-x64", "native", "librdkafka.dll");
    }
    else if (OperatingSystem.IsMacOS())
    {
        var rid = arch == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        yield return Path.Combine(baseDir, "runtimes", rid, "native", "librdkafka.dylib");
    }
}
