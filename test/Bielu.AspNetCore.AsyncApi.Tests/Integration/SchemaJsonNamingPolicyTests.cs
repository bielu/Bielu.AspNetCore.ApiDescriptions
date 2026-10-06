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
/// Verifies that <see cref="Bielu.AspNetCore.AsyncApi.Services.AsyncApiOptions.AsyncApiJsonSchemaJsonOptions"/>
/// lets a document override the property naming policy used for JSON schema generation independently of the
/// app-wide <see cref="Microsoft.AspNetCore.Http.Json.JsonOptions"/> (which defaults to camelCase).
/// </summary>
public class SchemaJsonNamingPolicyTests
{
    private const string TestDocumentName = "v1";

    [Fact]
    public async Task GetAsyncApiDocument_WithNullPropertyNamingPolicy_UsesPascalCasePropertyNames()
    {
        using var host = await Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddRouting();
                    services.AddAsyncApi(TestDocumentName, options =>
                    {
                        options.AsyncApiJsonSchemaJsonOptions = new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = null
                        };
                    });
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

        response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
        var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        var schemas = root.GetProperty("components").GetProperty("schemas");
        schemas.TryGetProperty("namingPolicyTestMessage", out var schema).ShouldBeTrue(content);

        var properties = schema.GetProperty("properties");

        properties.TryGetProperty("SomeValue", out _).ShouldBeTrue();
        properties.TryGetProperty("someValue", out _).ShouldBeFalse();
    }
}

[AsyncApi]
[Channel("namingPolicyTestBus")]
public class NamingPolicyTestBus
{
    [PublishOperation(typeof(NamingPolicyTestMessage))]
    public void ProcessMessage(NamingPolicyTestMessage message) { }
}

public class NamingPolicyTestMessage
{
    public string SomeValue { get; set; } = string.Empty;
}
