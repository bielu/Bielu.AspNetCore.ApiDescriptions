using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Scalar.Tests.Integration;

/// <summary>
/// The ArazzoScalar example's creation endpoints: a workflow reads a new resource's id from the body or follows its
/// <c>Location</c>, so both must name the same resource.
/// </summary>
public sealed class ArazzoScalarExampleIdentifierTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/orders", """{ "sku": "SKU-1", "quantity": 2 }""")]
    [InlineData("/payments/", """{ "orderId": "8d3f2c1e-0000-4000-8000-000000000001" }""")]
    [InlineData("/reservations/", """{ "orderId": "8d3f2c1e-0000-4000-8000-000000000001" }""")]
    public async Task CreationEndpoint_LocationHeader_EndsWithTheBodysId(string path, string request)
    {
        // Arrange
        using var client = factory.CreateClient();

        // Act
        using var response = await client.PostAsync(new Uri(path, UriKind.Relative), JsonContent.Create(JsonNode.Parse(request)));

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        var id = (string)(await response.Content.ReadFromJsonAsync<JsonNode>())!["id"]!;
        response.Headers.Location!.OriginalString.Split('/')[^1].ShouldBe(id);
    }
}
