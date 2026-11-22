using System.Text.Json.Nodes;
using Bielu.Arazzo.SourceResolvers;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Tests;

public class JsonSourceResolverTests
{
    private static readonly JsonNode OpenApi = JsonNode.Parse("""
        {
            "openapi": "3.1.0",
            "paths": {
                "/pets": { "summary": "not an operation", "get": { "operationId": "listPets" } },
                "/pets/{id}": { "parameters": [], "delete": { "operationId": "deletePet" } }
            },
            "webhooks": { "newPet": { "post": { "operationId": "onNewPet" } } }
        }
        """)!;

    private static readonly JsonNode AsyncApi3 = JsonNode.Parse("""
        {
            "asyncapi": "3.0.0",
            "channels": { "user/signedup": { "address": "user/signedup" } },
            "operations": { "onSignup": { "action": "receive" } }
        }
        """)!;

    private static readonly JsonNode AsyncApi2 = JsonNode.Parse("""
        {
            "asyncapi": "2.6.0",
            "channels": {
                "orders": { "publish": { "operationId": "placeOrder" }, "subscribe": { "operationId": "orderPlaced" } }
            }
        }
        """)!;

    [Theory]
    [InlineData("listPets")]
    [InlineData("deletePet")]
    [InlineData("onNewPet")]
    public void OpenApi_TryResolveOperation_FindsOperationsInPathsAndWebhooks(string operationId)
    {
        // Act
        var found = new OpenApiJsonSourceResolver().TryResolveOperation(OpenApi, operationId, out var operation);

        // Assert
        found.ShouldBeTrue();
        operation!["operationId"]!.GetValue<string>().ShouldBe(operationId);
    }

    [Fact]
    public void OpenApi_TryResolveOperation_UnknownId_ReturnsFalse()
    {
        new OpenApiJsonSourceResolver().TryResolveOperation(OpenApi, "nope", out var operation).ShouldBeFalse();
        operation.ShouldBeNull();
    }

    [Theory]
    [InlineData("/paths/~1pets/get", true)]
    [InlineData("/paths/~1pets~1%7Bid%7D/delete", true)]
    [InlineData("/paths/~1pets~1{id}/delete", true)]
    [InlineData("/paths/~1pets/post", false)]
    [InlineData("/paths/~1pets/summary", false)]
    [InlineData("/paths/~2pets/get", false)]
    [InlineData("paths/~1pets/get", false)]
    [InlineData("", false)]
    public void OpenApi_TryResolveOperationPath_ResolvesPointersToObjects(string pointer, bool expected)
    {
        new OpenApiJsonSourceResolver().TryResolveOperationPath(OpenApi, pointer, out _).ShouldBe(expected);
    }

    [Fact]
    public void OpenApi_SourceType_IsOpenApi()
    {
        new OpenApiJsonSourceResolver().SourceType.ShouldBe("openapi");
    }

    [Theory]
    [InlineData("onSignup")]
    public void AsyncApi3_TryResolveOperation_FindsOperationKeys(string operationId)
    {
        new AsyncApiJsonSourceResolver().TryResolveOperation(AsyncApi3, operationId, out var operation).ShouldBeTrue();
        operation!["action"]!.GetValue<string>().ShouldBe("receive");
    }

    [Theory]
    [InlineData("placeOrder")]
    [InlineData("orderPlaced")]
    public void AsyncApi2_TryResolveOperation_FindsChannelOperationIds(string operationId)
    {
        new AsyncApiJsonSourceResolver().TryResolveOperation(AsyncApi2, operationId, out var operation).ShouldBeTrue();
        operation!["operationId"]!.GetValue<string>().ShouldBe(operationId);
    }

    [Fact]
    public void AsyncApi_TryResolveOperation_UnknownId_ReturnsFalse()
    {
        new AsyncApiJsonSourceResolver().TryResolveOperation(AsyncApi3, "nope", out _).ShouldBeFalse();
        new AsyncApiJsonSourceResolver().TryResolveOperation(AsyncApi2, "nope", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("/channels/user~1signedup", true)]
    [InlineData("/channels/user~1signedup/address", false)]
    [InlineData("/channels/orders", false)]
    public void AsyncApi_TryResolveChannelPath_ResolvesPointersToObjects(string pointer, bool expected)
    {
        new AsyncApiJsonSourceResolver().TryResolveChannelPath(AsyncApi3, pointer, out _).ShouldBe(expected);
    }

    [Fact]
    public void AsyncApi_TryResolveOperationPath_ResolvesOperationsPointer()
    {
        new AsyncApiJsonSourceResolver().TryResolveOperationPath(AsyncApi3, "/operations/onSignup", out _).ShouldBeTrue();
        new AsyncApiJsonSourceResolver().TryResolveOperationPath(AsyncApi3, "/operations/0", out _).ShouldBeFalse();
    }

    [Fact]
    public void Pointer_IntoArray_UsesIndex()
    {
        var document = JsonNode.Parse("""{ "tags": [ { "name": "a" } ] }""")!;
        new AsyncApiJsonSourceResolver().TryResolveChannelPath(document, "/tags/0", out var tag).ShouldBeTrue();
        tag!["name"]!.GetValue<string>().ShouldBe("a");
        new AsyncApiJsonSourceResolver().TryResolveChannelPath(document, "/tags/1", out _).ShouldBeFalse();
        new AsyncApiJsonSourceResolver().TryResolveChannelPath(document, "/tags/-1", out _).ShouldBeFalse();
    }

    [Fact]
    public void Resolvers_ThrowForNullArguments()
    {
        var openApi = new OpenApiJsonSourceResolver();
        var asyncApi = new AsyncApiJsonSourceResolver();

        Should.Throw<ArgumentNullException>(() => openApi.TryResolveOperation(null!, "x", out _));
        Should.Throw<ArgumentNullException>(() => openApi.TryResolveOperation(OpenApi, null!, out _));
        Should.Throw<ArgumentNullException>(() => openApi.TryResolveOperationPath(null!, "/x", out _));
        Should.Throw<ArgumentNullException>(() => asyncApi.TryResolveOperation(null!, "x", out _));
        Should.Throw<ArgumentNullException>(() => asyncApi.TryResolveOperationPath(AsyncApi3, null!, out _));
        Should.Throw<ArgumentNullException>(() => asyncApi.TryResolveChannelPath(null!, "/x", out _));
    }

    [Fact]
    public void Workspace_SourceNames_ListsRegisteredSourcesInOrder()
    {
        // Arrange
        var workspace = new ArazzoWorkspace();
        workspace.RegisterDocument("b", "openapi", OpenApi);
        workspace.RegisterDocument("a", "asyncapi", AsyncApi3);

        // Act / Assert
        workspace.SourceNames.ShouldBe(["b", "a"]);
    }
}
