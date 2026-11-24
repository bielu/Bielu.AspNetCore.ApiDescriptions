using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Scalar.Tests.Integration;

/// <summary>The ArazzoScalar example's OpenAPI documents, one per service its workflows call.</summary>
public sealed class ArazzoScalarExampleDocumentsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] Documents = ["v1", "payments", "inventory", "email"];

    [Fact]
    public async Task OpenApiDocuments_EachOperation_IsInExactlyOneDocument()
    {
        // Arrange
        using var client = factory.CreateClient();

        // Act
        var owners = new Dictionary<string, List<string>>();
        foreach (var name in Documents)
        {
            var document = JsonNode.Parse(await client.GetStringAsync(new Uri($"/openapi/{name}.json", UriKind.Relative)))!;
            var operationIds = document["paths"]!.AsObject()
                .SelectMany(path => path.Value!.AsObject())
                .Select(operation => (string?)operation.Value?["operationId"])
                .OfType<string>();
            foreach (var operationId in operationIds)
            {
                (owners.TryGetValue(operationId, out var list) ? list : owners[operationId] = []).Add(name);
            }
        }

        // Assert: a bare operationId in a workflow resolves to one service, and each order operation to the orders one.
        owners.Where(owner => owner.Value.Count != 1).ShouldBeEmpty();
        owners["createOrder"].ShouldBe(["v1"]);
        owners["getOrder"].ShouldBe(["v1"]);
        owners["submitOrder"].ShouldBe(["v1"]);
        owners["authorizePayment"].ShouldBe(["payments"]);
        owners["createReservation"].ShouldBe(["inventory"]);
        owners["sendEmail"].ShouldBe(["email"]);
    }
}
