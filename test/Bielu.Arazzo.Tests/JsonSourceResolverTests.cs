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
    [InlineData("/paths/~1pets", false)]
    [InlineData("/webhooks/newPet/post", true)]
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

    [Theory]
    [InlineData("/operations/onSignup", true)]
    [InlineData("/components/operations/audit", true)]
    [InlineData("/channels/user~1signedup", false)]
    [InlineData("/components/channels/shared", false)]
    [InlineData("/info", false)]
    public void AsyncApi3_TryResolveOperationPath_AcceptsOnlyOperations(string pointer, bool expected)
    {
        // Arrange
        var document = JsonNode.Parse("""
            {
                "asyncapi": "3.0.0",
                "info": { "title": "t" },
                "channels": { "user/signedup": { "address": "user/signedup" } },
                "operations": { "onSignup": { "action": "receive" } },
                "components": { "channels": { "shared": {} }, "operations": { "audit": { "action": "send" } } }
            }
            """)!;

        // Act & Assert
        new AsyncApiJsonSourceResolver().TryResolveOperationPath(document, pointer, out _).ShouldBe(expected);
    }

    [Theory]
    [InlineData("/channels/orders/publish", true)]
    [InlineData("/channels/orders/subscribe", true)]
    [InlineData("/channels/orders", false)]
    public void AsyncApi2_TryResolveOperationPath_AcceptsChannelOperations(string pointer, bool expected)
    {
        new AsyncApiJsonSourceResolver().TryResolveOperationPath(AsyncApi2, pointer, out _).ShouldBe(expected);
    }

    [Theory]
    [InlineData("/channels/user~1signedup", true)]
    [InlineData("/components/channels/shared", true)]
    [InlineData("/operations/onSignup", false)]
    [InlineData("/tags/0", false)]
    public void AsyncApi_TryResolveChannelPath_AcceptsOnlyChannels(string pointer, bool expected)
    {
        // Arrange
        var document = JsonNode.Parse("""
            {
                "asyncapi": "3.0.0",
                "tags": [ { "name": "a" } ],
                "channels": { "user/signedup": { "address": "user/signedup" } },
                "operations": { "onSignup": { "action": "receive" } },
                "components": { "channels": { "shared": {} } }
            }
            """)!;

        // Act & Assert
        new AsyncApiJsonSourceResolver().TryResolveChannelPath(document, pointer, out _).ShouldBe(expected);
    }

    [Fact]
    public void Pointer_IntoArray_UsesIndex()
    {
        var document = JsonNode.Parse("""{ "tags": [ { "name": "a" } ] }""")!;
        JsonDocumentPointer.EvaluateObject(document, "/tags/0")!["name"]!.GetValue<string>().ShouldBe("a");
        JsonDocumentPointer.EvaluateObject(document, "/tags/1").ShouldBeNull();
        JsonDocumentPointer.EvaluateObject(document, "/tags/-1").ShouldBeNull();
    }

    [Fact]
    public void Pointer_WithMalformedEscape_HasNoTokens()
    {
        JsonDocumentPointer.Tokens("/a~2b").ShouldBeNull();
        JsonDocumentPointer.Tokens("a").ShouldBeNull();
        JsonDocumentPointer.Tokens("/a~1b/c~0d").ShouldBe(["a/b", "c~d"]);
    }

    private static readonly JsonNode OpenApiWithPathItemReferences = JsonNode.Parse("""
        {
            "openapi": "3.1.0",
            "paths": {
                "/pets": { "$ref": "#/components/pathItems/Pets" },
                "/loop": { "$ref": "#/components/pathItems/Loop" },
                "/outside": { "$ref": "other.json#/Pets" }
            },
            "components": {
                "pathItems": {
                    "Pets": { "get": { "operationId": "listPets" } },
                    "Loop": { "$ref": "#/paths/~1loop" }
                },
                "schemas": { "Pet": { "properties": { "get": { "type": "string" } } } }
            }
        }
        """)!;

    [Fact]
    public void OpenApi_TryResolveOperation_FollowsPathItemReferences()
    {
        // Act & Assert
        var resolver = new OpenApiJsonSourceResolver();
        resolver.TryResolveOperation(OpenApiWithPathItemReferences, "listPets", out var operation).ShouldBeTrue();
        operation!["operationId"]!.GetValue<string>().ShouldBe("listPets");
        resolver.TryResolveOperation(OpenApiWithPathItemReferences, "nope", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("/paths/~1pets/get", true)]
    [InlineData("/components/pathItems/Pets/get", true)]
    [InlineData("/paths/~1pets", false)]
    [InlineData("/paths/~1loop/get", false)]
    [InlineData("/paths/~1outside/get", false)]
    [InlineData("/components/schemas/Pet/properties/get", false)]
    public void OpenApi_TryResolveOperationPath_FollowsPathItemReferences(string pointer, bool expected)
    {
        new OpenApiJsonSourceResolver().TryResolveOperationPath(OpenApiWithPathItemReferences, pointer, out _).ShouldBe(expected);
    }

    [Fact]
    public void Resolvers_MalformedDocuments_ResolveNothingInsteadOfThrowing()
    {
        // Arrange: operations and channels that are not objects, and documents that are not objects at all.
        var openApi = JsonNode.Parse("""{ "openapi": "3.1.0", "paths": { "/pets": { "get": true }, "/x": [] } }""")!;
        var asyncApi = JsonNode.Parse("""{ "asyncapi": "2.6.0", "operations": { "o": 1 }, "channels": { "c": true, "d": { "publish": "x" } } }""")!;
        var array = JsonNode.Parse("[]")!;

        // Act & Assert
        new OpenApiJsonSourceResolver().TryResolveOperation(openApi, "listPets", out _).ShouldBeFalse();
        new OpenApiJsonSourceResolver().TryResolveOperation(array, "listPets", out _).ShouldBeFalse();
        new AsyncApiJsonSourceResolver().TryResolveOperation(asyncApi, "o", out _).ShouldBeFalse();
        new AsyncApiJsonSourceResolver().TryResolveOperation(array, "o", out _).ShouldBeFalse();
    }

    private static readonly JsonNode OpenApi32 = JsonNode.Parse("""
        {
            "openapi": "3.2.0",
            "paths": { "/pets": { "additionalOperations": { "COPY": { "operationId": "copyPet" } } } },
            "components": { "pathItems": { "Pets": { "additionalOperations": { "LINK": { "operationId": "linkPet" } } } } }
        }
        """)!;

    [Fact]
    public void OpenApi_TryResolveOperation_FindsAdditionalOperations()
    {
        new OpenApiJsonSourceResolver().TryResolveOperation(OpenApi32, "copyPet", out var operation).ShouldBeTrue();
        operation!["operationId"]!.GetValue<string>().ShouldBe("copyPet");
    }

    [Theory]
    [InlineData("/paths/~1pets/additionalOperations/COPY", true)]
    [InlineData("/components/pathItems/Pets/additionalOperations/LINK", true)]
    [InlineData("/paths/~1pets/additionalOperations/MOVE", false)]
    [InlineData("/paths/~1pets/additionalOperations", false)]
    public void OpenApi_TryResolveOperationPath_ResolvesAdditionalOperations(string pointer, bool expected)
    {
        new OpenApiJsonSourceResolver().TryResolveOperationPath(OpenApi32, pointer, out _).ShouldBe(expected);
    }

    private static readonly JsonNode AsyncApiWithReferences = JsonNode.Parse("""
        {
            "asyncapi": "3.0.0",
            "channels": {
                "orders": { "$ref": "#/components/channels/orders" },
                "dangling": { "$ref": "#/components/channels/missing" },
                "loop": { "$ref": "#/channels/loop" }
            },
            "operations": {
                "placeOrder": { "$ref": "#/components/operations/placeOrder" },
                "broken": { "$ref": "other.yaml#/operations/x" }
            },
            "components": {
                "channels": { "orders": { "address": "orders" } },
                "operations": { "placeOrder": { "action": "send" } }
            }
        }
        """)!;

    [Theory]
    [InlineData("/channels/orders", true)]
    [InlineData("/channels/dangling", false)]
    [InlineData("/channels/loop", false)]
    public void AsyncApi_TryResolveChannelPath_FollowsLocalReferences(string pointer, bool expected)
    {
        new AsyncApiJsonSourceResolver().TryResolveChannelPath(AsyncApiWithReferences, pointer, out _).ShouldBe(expected);
    }

    [Theory]
    [InlineData("/operations/placeOrder", true)]
    [InlineData("/operations/broken", false)]
    public void AsyncApi_TryResolveOperationPath_FollowsLocalReferences(string pointer, bool expected)
    {
        new AsyncApiJsonSourceResolver().TryResolveOperationPath(AsyncApiWithReferences, pointer, out _).ShouldBe(expected);
    }

    [Fact]
    public void AsyncApi_TryResolveOperation_FollowsLocalReferences()
    {
        // Act & Assert
        var resolver = new AsyncApiJsonSourceResolver();
        resolver.TryResolveOperation(AsyncApiWithReferences, "placeOrder", out var operation).ShouldBeTrue();
        operation!["action"]!.GetValue<string>().ShouldBe("send");
        resolver.TryResolveOperation(AsyncApiWithReferences, "broken", out _).ShouldBeFalse();
    }

    [Fact]
    public void AsyncApi2_ChannelReference_IsFollowedForItsOperations()
    {
        // Arrange
        var document = JsonNode.Parse("""
            {
                "asyncapi": "2.6.0",
                "channels": { "orders": { "$ref": "#/components/channels/orders" } },
                "components": { "channels": { "orders": { "publish": { "operationId": "placeOrder" } } } }
            }
            """)!;

        // Act & Assert
        var resolver = new AsyncApiJsonSourceResolver();
        resolver.TryResolveOperation(document, "placeOrder", out _).ShouldBeTrue();
        resolver.TryResolveOperationPath(document, "/channels/orders/publish", out _).ShouldBeTrue();
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
