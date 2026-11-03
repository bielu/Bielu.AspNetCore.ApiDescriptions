using System.ComponentModel;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Regression tests for properties whose type gets a schema reference id (enums and named
/// complex types). Their description and default used to be written for a <c>$ref</c> site:
/// the description to an <c>x-ref-description</c> annotation nothing read back, the default to
/// the <c>$ref</c> keyword itself. Schemas are never componentized, so both were lost, and only
/// primitive properties kept their description.
/// </summary>
public class ReferenceTypedPropertySchemaTests
{
    private const string TestDocumentName = "v1";

    [Fact]
    public async Task GetAsyncApiDocument_EnumProperty_KeepsDescription()
    {
        var properties = await GetMessagePropertiesAsync();

        properties.GetProperty("state").GetProperty("description").GetString().ShouldBe("The message's state.");
    }

    [Fact]
    public async Task GetAsyncApiDocument_NullableComplexProperty_KeepsDescription()
    {
        var properties = await GetMessagePropertiesAsync();

        properties.GetProperty("owner").GetProperty("description").GetString().ShouldBe("Who owns it, if anyone.");
    }

    [Fact]
    public async Task GetAsyncApiDocument_EnumPropertyWithDefaultValue_WritesDefaultNotRef()
    {
        var properties = await GetMessagePropertiesAsync();

        var priority = properties.GetProperty("priority");
        priority.TryGetProperty("$ref", out _).ShouldBeFalse();
        priority.GetProperty("default").ValueKind.ShouldNotBe(JsonValueKind.Undefined);
    }

    private static async Task<JsonElement> GetMessagePropertiesAsync()
    {
        using var host = await Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddRouting();
                    services.AddAsyncApi(TestDocumentName);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapAsyncApi();
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestServer().CreateClient();

        var response = await client.GetAsync($"/asyncapi/{TestDocumentName}.json");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var root = JsonDocument.Parse(content).RootElement;
        var schemas = root.GetProperty("components").GetProperty("schemas");
        schemas.TryGetProperty("referenceTypedPropertyTestMessage", out var schema).ShouldBeTrue();
        return schema.GetProperty("properties").Clone();
    }
}

[AsyncApi]
[Channel("referenceTypedPropertyTestBus")]
public class ReferenceTypedPropertyTestBus
{
    [ReceiveOperation(typeof(ReferenceTypedPropertyTestMessage))]
    public void ProcessMessage(ReferenceTypedPropertyTestMessage message) { }
}

public class ReferenceTypedPropertyTestMessage
{
    [Description("The message's state.")]
    public ReferenceTypedPropertyTestState State { get; set; }

    [DefaultValue(ReferenceTypedPropertyTestState.Open)]
    public ReferenceTypedPropertyTestState Priority { get; set; }

    [Description("Who owns it, if anyone.")]
    public ReferenceTypedPropertyTestOwner? Owner { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<ReferenceTypedPropertyTestState>))]
public enum ReferenceTypedPropertyTestState
{
    Open,
    Closed,
}

public class ReferenceTypedPropertyTestOwner
{
    public string Name { get; set; } = string.Empty;
}
