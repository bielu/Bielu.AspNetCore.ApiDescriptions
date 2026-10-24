using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI;
using Json.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.CloudEvents.Tests.Integration;

public class CloudEventsDocumentTests
{
    private const string DocumentName = "cloudevents";

    public sealed record OrderPlaced(string OrderId);

    public sealed record OrderAudited(string OrderId, string Auditor);

    public sealed record AuditHeaders(string TenantId);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("orders/placed")]
        [Message(typeof(OrderPlaced), MessageId = "orderPlaced")]
        [SubscribeOperation(typeof(OrderPlaced), OperationId = "orderPlacedOperation")]
        public void OrderPlacedEvent()
        {
        }

        [Channel("orders/audited")]
        [Message(typeof(OrderAudited), MessageId = "orderAudited", HeadersType = typeof(AuditHeaders))]
        [SubscribeOperation(typeof(OrderAudited), OperationId = "orderAuditedOperation")]
        public void OrderAuditedEvent()
        {
        }
    }

    [Fact]
    public async Task Binary_OnHttpServer_AddsCeHeaders()
    {
        // Act
        using var document = await GetDocumentAsync(options => options.AddCloudEvents());

        // Assert
        var headers = Message(document, "orderPlaced").GetProperty("headers");
        headers.GetProperty("type").GetString().ShouldBe("object");
        Required(headers).ShouldBe(["ce-id", "ce-source", "ce-specversion", "ce-type"], ignoreOrder: true);
        var properties = headers.GetProperty("properties");
        properties.GetProperty("ce-specversion").GetProperty("const").GetString().ShouldBe("1.0");
        properties.GetProperty("ce-type").GetProperty("const").GetString().ShouldBe("orderPlaced");
        properties.GetProperty("ce-source").GetProperty("format").GetString().ShouldBe("uri-reference");
        properties.GetProperty("ce-time").GetProperty("format").GetString().ShouldBe("date-time");
        properties.TryGetProperty("ce-datacontenttype", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("kafka", "ce_id")]
    [InlineData("amqp", "cloudEvents:id")]
    [InlineData("mqtt5", "id")]
    [InlineData("ws", "ce-id")]
    public async Task Binary_WithAutoBinding_NamesHeadersAfterTheServerProtocol(string protocol, string idHeader)
    {
        // Act
        using var document = await GetDocumentAsync(options => options.AddCloudEvents(), protocol);

        // Assert
        Required(Message(document, "orderPlaced").GetProperty("headers")).ShouldContain(idHeader);
    }

    [Fact]
    public async Task Binary_WithServersOfDifferentProtocols_FallsBackToHttp()
    {
        // Act
        using var document = await GetDocumentAsync(options => options
            .AddServer("kafka", "broker:9092", "kafka")
            .AddCloudEvents());

        // Assert
        Required(Message(document, "orderPlaced").GetProperty("headers")).ShouldContain("ce-id");
    }

    [Fact]
    public async Task Binary_WithExplicitBinding_IgnoresTheServerProtocol()
    {
        // Act
        using var document = await GetDocumentAsync(options => options
            .AddCloudEvents(cloudEvents => cloudEvents.UseProtocolBinding(CloudEventsProtocolBinding.Kafka)));

        // Assert
        Required(Message(document, "orderPlaced").GetProperty("headers")).ShouldContain("ce_type");
    }

    [Fact]
    public async Task Binary_WithExistingHeaders_CombinesThemWithAllOf()
    {
        // Act
        using var document = await GetDocumentAsync(options => options.AddCloudEvents());

        // Assert
        var allOf = Message(document, "orderAudited").GetProperty("headers").GetProperty("allOf");
        allOf.GetArrayLength().ShouldBe(2);
        allOf[0].GetProperty("$ref").GetString().ShouldEndWith("/AuditHeaders");
        Required(allOf[1]).ShouldContain("ce-id");
    }

    [Fact]
    public async Task Binary_WithSourceAndTypePrefix_PinsThem()
    {
        // Act
        using var document = await GetDocumentAsync(options => options.AddCloudEvents(cloudEvents => cloudEvents
            .WithSource("/orders")
            .WithTypePrefix("com.example.")));

        // Assert
        var properties = Message(document, "orderPlaced").GetProperty("headers").GetProperty("properties");
        properties.GetProperty("ce-type").GetProperty("const").GetString().ShouldBe("com.example.orderPlaced");
        properties.GetProperty("ce-source").GetProperty("const").GetString().ShouldBe("/orders");
    }

    [Fact]
    public async Task Binary_WithMessageExample_AddsTheAttributesToItsHeaders()
    {
        // Act
        using var document = await GetDocumentAsync(options => options
            .AddMessageExample("placed", new OrderPlaced("42"))
            .AddCloudEvents(cloudEvents => cloudEvents.WithSource("/orders")));

        // Assert
        var headers = Message(document, "orderPlaced").GetProperty("examples")[0].GetProperty("headers");
        headers.GetProperty("ce-specversion").GetString().ShouldBe("1.0");
        headers.GetProperty("ce-type").GetString().ShouldBe("orderPlaced");
        headers.GetProperty("ce-source").GetString().ShouldBe("/orders");
        headers.GetProperty("ce-id").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Structured_WrapsThePayloadInTheEnvelope()
    {
        // Act
        using var document = await GetDocumentAsync(options => options
            .AddCloudEvents(cloudEvents => cloudEvents.UseMode(CloudEventsMode.Structured)));

        // Assert
        var message = Message(document, "orderPlaced");
        message.GetProperty("contentType").GetString().ShouldBe("application/cloudevents+json");
        var payload = message.GetProperty("payload");
        Required(payload).ShouldBe(["id", "source", "specversion", "type"], ignoreOrder: true);
        var properties = payload.GetProperty("properties");
        properties.GetProperty("data").GetProperty("$ref").GetString().ShouldEndWith("/OrderPlaced");
        properties.GetProperty("datacontenttype").GetProperty("const").GetString().ShouldBe("application/json");
        properties.GetProperty("type").GetProperty("const").GetString().ShouldBe("orderPlaced");
        message.TryGetProperty("headers", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Structured_WithMessageExample_WrapsItsPayload()
    {
        // Act
        using var document = await GetDocumentAsync(options => options
            .AddMessageExample("placed", new OrderPlaced("42"))
            .AddCloudEvents(cloudEvents => cloudEvents.UseMode(CloudEventsMode.Structured)));

        // Assert
        var payload = Message(document, "orderPlaced").GetProperty("examples")[0].GetProperty("payload");
        payload.GetProperty("specversion").GetString().ShouldBe("1.0");
        payload.GetProperty("type").GetString().ShouldBe("orderPlaced");
        payload.GetProperty("datacontenttype").GetString().ShouldBe("application/json");
        payload.GetProperty("data").GetProperty("orderId").GetString().ShouldBe("42");
    }

    [Fact]
    public async Task ConfigureMessage_OverridesTheAttributesOfOneMessage()
    {
        // Act
        using var document = await GetDocumentAsync(options => options.AddCloudEvents(cloudEvents => cloudEvents
            .WithSource("/orders")
            .ConfigureMessage("orderAudited", message =>
            {
                message.Type = "com.example.audit.v2";
                message.Source = "/audit";
                message.DataSchema = "https://example.com/schemas/audit.json";
                message.Mode = CloudEventsMode.Structured;
            })));

        // Assert
        var audited = Message(document, "orderAudited");
        audited.GetProperty("contentType").GetString().ShouldBe("application/cloudevents+json");
        var properties = audited.GetProperty("payload").GetProperty("properties");
        properties.GetProperty("type").GetProperty("const").GetString().ShouldBe("com.example.audit.v2");
        properties.GetProperty("source").GetProperty("const").GetString().ShouldBe("/audit");
        properties.GetProperty("dataschema").GetProperty("const").GetString().ShouldBe("https://example.com/schemas/audit.json");

        var placed = Message(document, "orderPlaced").GetProperty("headers").GetProperty("properties");
        placed.GetProperty("ce-source").GetProperty("const").GetString().ShouldBe("/orders");
    }

    [Fact]
    public async Task MessageFilter_LeavesOtherMessagesAlone()
    {
        // Act
        using var document = await GetDocumentAsync(options => options.AddCloudEvents(cloudEvents =>
            cloudEvents.MessageFilter = key => key == "orderPlaced"));

        // Assert
        Message(document, "orderPlaced").GetProperty("headers").TryGetProperty("properties", out _).ShouldBeTrue();
        Message(document, "orderAudited").GetProperty("headers").TryGetProperty("allOf", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(AsyncApiVersion.AsyncApi2_0, CloudEventsMode.Binary)]
    [InlineData(AsyncApiVersion.AsyncApi3_0, CloudEventsMode.Binary)]
    [InlineData(AsyncApiVersion.AsyncApi2_0, CloudEventsMode.Structured)]
    [InlineData(AsyncApiVersion.AsyncApi3_0, CloudEventsMode.Structured)]
    public async Task ServedDocument_ConformsToOfficialSchema(AsyncApiVersion version, CloudEventsMode mode)
    {
        // Act
        using var document = await GetDocumentAsync(options =>
        {
            options.AsyncApiVersion = version;
            options.AddMessageExample("placed", new OrderPlaced("42"));
            options.AddCloudEvents(cloudEvents => cloudEvents.UseMode(mode).WithSource("/orders"));
        });

        // Assert
        var declaredVersion = document.RootElement.GetProperty("asyncapi").GetString()!;
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Conformance", "asyncapi", $"{declaredVersion}.json");
        var result = JsonSchema.FromText(File.ReadAllText(schemaPath))
            .Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        result.IsValid.ShouldBeTrue(string.Join(Environment.NewLine, result.Details
            .Where(detail => detail.Errors is { Count: > 0 })
            .SelectMany(detail => detail.Errors!.Select(error => $"{detail.InstanceLocation} {error.Value}"))));
    }

    private static JsonElement Message(JsonDocument document, string key) =>
        document.RootElement.GetProperty("components").GetProperty("messages").GetProperty(key);

    private static string[] Required(JsonElement schema) =>
        schema.GetProperty("required").EnumerateArray().Select(name => name.GetString()!).ToArray();

    private static async Task<JsonDocument> GetDocumentAsync(Action<AsyncApiOptions> configure, string protocol = "http")
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName, options =>
        {
            options.WithInfo("CloudEvents", "1.0.0");
            options.AddServer("main", "localhost", protocol);
            configure(options);
        });

        await using var app = builder.Build();
        app.MapAsyncApi();
        await app.StartAsync();

        var json = await app.GetTestClient().GetStringAsync(
            AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", DocumentName));
        await app.StopAsync();
        return JsonDocument.Parse(json);
    }
}
