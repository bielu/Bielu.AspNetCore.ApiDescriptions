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
/// Regression tests for bugs where nullability was silently dropped from generated schemas:
/// <c>NormalizeSchemaTypes</c> (in AsyncApiJsonSchemaService) used to collapse a nullable
/// property's exported "type": ["string", "null"] down to a single non-null type, and
/// <c>PruneNullTypeForComponentizedTypes</c> used to strip "null" from any nullable property
/// whose value type would be componentized (i.e. any named complex/object type), on the
/// assumption that a since-removed oneOf-wrapping step would restore it at the reference site.
/// That restoration step never worked (it referenced a nonexistent <c>Metadata</c> property and
/// the wrong constants class), so nested nullable object properties permanently lost their
/// nullability.
/// </summary>
public class NullablePropertySchemaTests
{
    private const string TestDocumentName = "v1";

    [Fact]
    public async Task GetAsyncApiDocument_NullableProperty_IsNotRequiredAndKeepsNullType()
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
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        var schemas = root.GetProperty("components").GetProperty("schemas");
        schemas.TryGetProperty("nullablePropertyTestMessage", out var schema).ShouldBeTrue();

        var properties = schema.GetProperty("properties");

        // Non-nullable property should have a plain "string" type.
        properties.GetProperty("requiredText").GetProperty("type").GetString().ShouldBe("string");

        // Nullable property should keep both "string" and "null" in its type.
        var nullableTextType = properties.GetProperty("nullableText").GetProperty("type");
        var nullableTextTypes = nullableTextType.ValueKind == JsonValueKind.Array
            ? nullableTextType.EnumerateArray().Select(t => t.GetString()).ToArray()
            : [nullableTextType.GetString()];
        nullableTextTypes.ShouldContain("string");
        nullableTextTypes.ShouldContain("null");

        // Nullable nested complex/object property should keep both "object" and "null" in its
        // type, even though it will be assigned a schema reference id (x-schema-id) as a named type.
        var nullableComplexType = properties.GetProperty("nullableComplex").GetProperty("type");
        var nullableComplexTypes = nullableComplexType.ValueKind == JsonValueKind.Array
            ? nullableComplexType.EnumerateArray().Select(t => t.GetString()).ToArray()
            : [nullableComplexType.GetString()];
        nullableComplexTypes.ShouldContain("object");
        nullableComplexTypes.ShouldContain("null");
    }
}

[AsyncApi]
[Channel("nullablePropertyTestBus")]
public class NullablePropertyTestBus
{
    [PublishOperation(typeof(NullablePropertyTestMessage))]
    public void ProcessMessage(NullablePropertyTestMessage message) { }
}

public class NullablePropertyTestMessage
{
    public string RequiredText { get; set; } = string.Empty;

    public string? NullableText { get; set; }

    public NullablePropertyTestComplex? NullableComplex { get; set; }
}

public class NullablePropertyTestComplex
{
    public string Name { get; set; } = string.Empty;
}
