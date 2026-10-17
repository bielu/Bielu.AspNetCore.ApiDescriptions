using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
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
/// Proves that data-annotation validation attributes and <see cref="DefaultValueAttribute"/> on message payload
/// properties survive the whole pipeline (export, model round-trip, serialization) into the served document.
/// </summary>
public class ValidationAttributeSchemaTests
{
    private const string TestDocumentName = "v1";

    private static async Task<JsonElement> GetPayloadPropertiesAsync()
    {
        using var host = await Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAsyncApi(TestDocumentName);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapAsyncApi());
                });
            })
            .StartAsync();

        var client = host.GetTestServer().CreateClient();
        var response = await client.GetAsync($"/asyncapi/{TestDocumentName}.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        root.GetProperty("components").GetProperty("schemas")
            .TryGetProperty("validatedOrderMessage", out var schema).ShouldBeTrue();
        return schema.GetProperty("properties").Clone();
    }

    [Fact]
    public async Task GetAsyncApiDocument_RangeAttributes_EmitNumericBounds()
    {
        // Act
        var properties = await GetPayloadPropertiesAsync();

        // Assert
        var quantity = properties.GetProperty("quantity");
        quantity.GetProperty("minimum").GetDecimal().ShouldBe(1m);
        quantity.GetProperty("maximum").GetDecimal().ShouldBe(100m);

        var price = properties.GetProperty("price");
        price.GetProperty("minimum").GetDecimal().ShouldBe(0.5m);
        price.GetProperty("maximum").GetDecimal().ShouldBe(999.5m);
    }

    [Fact]
    public async Task GetAsyncApiDocument_StringAttributes_EmitLengthPatternAndFormat()
    {
        // Act
        var properties = await GetPayloadPropertiesAsync();

        // Assert
        var sku = properties.GetProperty("sku");
        sku.GetProperty("pattern").GetString().ShouldBe("^[A-Z]{3}-\\d+$");
        sku.GetProperty("minLength").GetInt32().ShouldBe(5);
        sku.GetProperty("maxLength").GetInt32().ShouldBe(12);

        properties.GetProperty("trackingUrl").GetProperty("format").GetString().ShouldBe("uri");
        properties.GetProperty("signature").GetProperty("format").GetString().ShouldBe("byte");
    }

    [Fact]
    public async Task GetAsyncApiDocument_LengthAttributesOnCollection_EmitItemCounts()
    {
        // Act
        var properties = await GetPayloadPropertiesAsync();

        // Assert
        var tags = properties.GetProperty("tags");
        tags.GetProperty("minItems").GetInt32().ShouldBe(1);
        tags.GetProperty("maxItems").GetInt32().ShouldBe(5);
    }

    [Fact]
    public async Task GetAsyncApiDocument_DefaultValueAttribute_EmitsDefault()
    {
        // Act
        var properties = await GetPayloadPropertiesAsync();

        // Assert
        properties.GetProperty("priority").GetProperty("default").GetInt32().ShouldBe(3);
    }
}

[AsyncApi]
[Channel("validatedOrders")]
public class ValidatedOrderBus
{
    [PublishOperation(typeof(ValidatedOrderMessage))]
    public void Publish(ValidatedOrderMessage message) { }
}

public class ValidatedOrderMessage
{
    [Range(1, 100)]
    public int Quantity { get; set; }

    [Range(0.5, 999.5)]
    public double Price { get; set; }

    [RegularExpression("^[A-Z]{3}-\\d+$")]
    [StringLength(12, MinimumLength = 5)]
    public string Sku { get; set; } = string.Empty;

    [Url]
    public string? TrackingUrl { get; set; }

    [Base64String]
    public string? Signature { get; set; }

    [MinLength(1)]
    [MaxLength(5)]
    public List<string> Tags { get; set; } = [];

    [DefaultValue(3)]
    public int Priority { get; set; } = 3;
}
