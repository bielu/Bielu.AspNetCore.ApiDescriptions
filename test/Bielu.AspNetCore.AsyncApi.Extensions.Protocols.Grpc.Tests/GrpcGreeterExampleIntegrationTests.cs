using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc.Tests;

/// <summary>
/// Boots the GrpcGreeter example application and verifies that the generated AsyncAPI document carries
/// the gRPC protocol bindings.
/// </summary>
public class GrpcGreeterExampleIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GrpcGreeterExampleIntegrationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task AsyncApiDocument_ContainsGrpcBindings()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/asyncapi/grpc.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        // Server binding.
        json["servers"]!["grpc"]!["bindings"]!["grpc"]!["services"]!
            .AsArray().Select(n => n!.GetValue<string>()).ShouldContain("greet.Greeter");

        // Channel binding: the service name.
        json["channels"]!["greeter"]!["bindings"]!["grpc"]!["service"]!
            .GetValue<string>().ShouldBe("greet.Greeter");

        // Operation binding: at least one operation declares a gRPC method.
        var operations = json["operations"]!.AsObject();
        operations.Select(kvp => kvp.Value!["bindings"]?["grpc"]?["method"]?.GetValue<string>())
            .Where(m => m is not null)
            .ShouldNotBeEmpty();
    }
}
