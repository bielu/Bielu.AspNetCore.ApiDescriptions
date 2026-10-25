using System.IO.Pipelines;
using System.Text.Json;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp.Tests.Integration;

/// <summary>Talks to the server through a real MCP client, over in-memory pipes.</summary>
public sealed class ApiDescriptionsMcpServerTests : IAsyncLifetime
{
    private const string DocumentName = "mcp";

    private WebApplication? _app;
    private McpClient? _client;

    public sealed record OrderPlaced(string OrderId, int Quantity);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("orders/placed")]
        [Message(typeof(OrderPlaced), MessageId = "orderPlaced")]
        [SendOperation(typeof(OrderPlaced), OperationId = "publishOrderPlaced", Summary = "Tells others an order was placed.")]
        public void OrderPlacedEvent()
        {
        }
    }

    private McpClient Client => _client!;

    public async Task InitializeAsync()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddOpenApi();
        builder.Services.AddAsyncApi(DocumentName, options => options.WithInfo("Orders", "1.0.0"));
        // A second Arazzo document without the required info, to see generation failures reported to the agent.
        builder.Services.AddArazzo("broken", options =>
        {
            options.ValidateSourceReferencesOnStartup = false;
            options.AddWorkflow("noop", workflow => workflow.Step("only", step => step.Workflow("noop")));
        });
        builder.Services.AddArazzo(options =>
        {
            options.ValidateSourceReferencesOnStartup = false;
            options.WithInfo("Order workflows", "1.0.0");
            options.AddWorkflow("noop", workflow => workflow.Step("only", step => step.Workflow("noop")));
        });
        builder.Services.AddMcpServer()
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .WithApiDescriptions();

        _app = builder.Build();
        await _app.StartAsync();

        _client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream(), loggerFactory: null));
    }

    public async Task DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task ListTools_ListsTheFourReadOnlyTools()
    {
        // Act
        var tools = await Client.ListToolsAsync();

        // Assert
        tools.Select(tool => tool.Name).ShouldBe(
            ["list_api_descriptions", "get_api_description", "find_api_operations", "get_api_operation", "list_asyncapi_operations", "get_asyncapi_message"],
            ignoreOrder: true);
        tools.ShouldAllBe(tool => tool.ProtocolTool.Annotations!.ReadOnlyHint == true);
    }

    [Theory]
    [InlineData("list_api_descriptions")]
    [InlineData("find_api_operations")]
    [InlineData("get_api_operation")]
    public async Task ListTools_StructuredToolsDeclareAnOutputSchema(string name)
    {
        // Act
        var tool = (await Client.ListToolsAsync()).Single(tool => tool.Name == name);

        // Assert
        tool.ProtocolTool.OutputSchema.ShouldNotBeNull();
        tool.ProtocolTool.OutputSchema.Value.GetProperty("type").GetString().ShouldBe("object");
    }

    [Fact]
    public async Task ListApiDescriptions_ListsEveryDocumentFamily()
    {
        // Act
        using var result = await CallJsonAsync("list_api_descriptions", new Dictionary<string, object?>());

        // Assert
        var documents = result.RootElement.GetProperty("documents").EnumerateArray().ToList();
        var uris = documents.Select(d => d.GetProperty("uri").GetString()).ToList();
        uris.ShouldContain("apidescription://openapi/v1");
        uris.ShouldContain($"apidescription://asyncapi/{DocumentName}");
        uris.ShouldContain("apidescription://arazzo/v1");
        var asyncApi = documents.Single(d => d.GetProperty("kind").GetString() == "AsyncAPI");
        asyncApi.GetProperty("mimeType").GetString().ShouldBe("application/vnd.aai.asyncapi+json");
        asyncApi.GetProperty("title").GetString().ShouldBe("Orders");
        asyncApi.GetProperty("specificationVersion").GetString().ShouldStartWith("3.");
        asyncApi.GetProperty("fingerprint").GetString().ShouldStartWith("sha256:");
        asyncApi.GetProperty("capabilities").EnumerateArray().Select(c => c.GetString()).ShouldContain("get_api_operation");
        documents.Single(d => d.GetProperty("kind").GetString() == "OpenAPI")
            .GetProperty("capabilities").EnumerateArray().Select(c => c.GetString()).ShouldBe(["get_api_description"]);
    }

    [Fact]
    public async Task ListApiDescriptions_DocumentThatFailsToGenerate_IsListedWithItsError()
    {
        // Act
        using var result = await CallJsonAsync("list_api_descriptions", new Dictionary<string, object?>());

        // Assert
        var broken = result.RootElement.GetProperty("documents").EnumerateArray().Single(d => d.GetProperty("name").GetString() == "broken");
        broken.GetProperty("error").GetString()!.ShouldContain("has no Info");
    }

    [Fact]
    public async Task FindApiOperations_FindsTheOperationAndReportsDocumentsItCannotSearch()
    {
        // Act
        using var result = await CallJsonAsync("find_api_operations", new() { ["query"] = "order placed" });

        // Assert
        var match = result.RootElement.GetProperty("matches").EnumerateArray().ShouldHaveSingleItem();
        match.GetProperty("handle").GetProperty("operation").GetString().ShouldBe("publishOrderPlaced");
        match.GetProperty("handle").GetProperty("document").GetString().ShouldBe(DocumentName);
        match.GetProperty("matchedTerms").GetInt32().ShouldBe(2);
        result.RootElement.GetProperty("notSearched").EnumerateArray().Select(d => d.GetProperty("kind").GetString())
            .ShouldBe(["OpenAPI", "Arazzo", "Arazzo"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("action", "sideways", "'action'")]
    [InlineData("cursor", "not-a-cursor", "'cursor'")]
    [InlineData("document", "nope", "No document matches")]
    public async Task FindApiOperations_BadArgument_IsAnError(string argument, string value, string expected)
    {
        // Act
        var result = await Client.CallToolAsync("find_api_operations", new Dictionary<string, object?> { [argument] = value });

        // Assert
        result.IsError.ShouldBe(true);
        Text(result).ShouldContain(expected);
    }

    [Fact]
    public async Task GetApiOperation_ReturnsTheContractWithItsReferences()
    {
        // Act
        using var result = await CallJsonAsync("get_api_operation",
            new() { ["kind"] = "asyncapi", ["document"] = DocumentName, ["operation"] = "publishOrderPlaced" });

        // Assert
        var root = result.RootElement;
        root.GetProperty("handle").GetProperty("kind").GetString().ShouldBe("AsyncAPI");
        root.GetProperty("action").GetString().ShouldBe("send");
        root.GetProperty("channel").GetProperty("address").GetString().ShouldBe("orders/placed");
        var schema = root.GetProperty("messages")[0].GetProperty("payload");
        var references = root.GetProperty("references");
        if (schema.TryGetProperty("$ref", out var reference))
        {
            schema = references.GetProperty(reference.GetString()!);
        }

        schema.GetProperty("properties").TryGetProperty("orderId", out _).ShouldBeTrue();
        root.GetProperty("notSpecified").EnumerateArray().Select(entry => entry.GetProperty("field").GetString())
            .ShouldContain("servers");
    }

    [Theory]
    [InlineData("OpenAPI", "v1", "op", "cannot be inspected yet")]
    [InlineData("AsyncAPI", "nope", "op", "No AsyncAPI document")]
    [InlineData("AsyncAPI", DocumentName, "nope", "find_api_operations")]
    [InlineData("AsyncAPI", DocumentName, null, "'operation' are required")]
    public async Task GetApiOperation_UnknownTarget_IsAnError(string kind, string document, string? operation, string expected)
    {
        // Act
        var result = await Client.CallToolAsync("get_api_operation",
            new Dictionary<string, object?> { ["kind"] = kind, ["document"] = document, ["operation"] = operation });

        // Assert
        result.IsError.ShouldBe(true);
        Text(result).ShouldContain(expected);
    }

    [Fact]
    public async Task GetApiOperation_CursorForAnotherVersionOfTheDocument_IsAnError()
    {
        // Act
        var result = await Client.CallToolAsync("get_api_operation", new Dictionary<string, object?>
        {
            ["kind"] = "AsyncAPI",
            ["document"] = DocumentName,
            ["operation"] = "publishOrderPlaced",
            ["cursor"] = Convert.ToBase64String("references|sha256:old|1"u8.ToArray()),
        });

        // Assert
        result.IsError.ShouldBe(true);
        Text(result).ShouldContain("changed");
    }

    [Theory]
    [InlineData("AsyncAPI", DocumentName, "asyncapi")]
    [InlineData("openapi", "V1", "openapi")]
    [InlineData("Arazzo", "v1", "arazzo")]
    public async Task GetApiDescription_ReturnsTheDocument(string kind, string name, string versionProperty)
    {
        // Act
        using var document = await CallJsonAsync("get_api_description", new() { ["kind"] = kind, ["name"] = name });

        // Assert
        document.RootElement.TryGetProperty(versionProperty, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task GetApiDescription_UnknownDocument_IsAnError()
    {
        // Act
        var result = await Client.CallToolAsync("get_api_description", new Dictionary<string, object?> { ["kind"] = "AsyncAPI", ["name"] = "nope" });

        // Assert
        result.IsError.ShouldBe(true);
        Text(result).ShouldContain("list_api_descriptions");
    }

    [Fact]
    public async Task GetApiDescription_DocumentThatFailsToGenerate_ReportsWhy()
    {
        // Act
        var result = await Client.CallToolAsync("get_api_description", new Dictionary<string, object?> { ["kind"] = "Arazzo", ["name"] = "broken" });

        // Assert
        result.IsError.ShouldBe(true);
        Text(result).ShouldContain("has no Info");
    }

    [Fact]
    public async Task GetApiDescription_MissingArguments_IsAnError()
    {
        // Act
        var result = await Client.CallToolAsync("get_api_description", new Dictionary<string, object?>());

        // Assert
        result.IsError.ShouldBe(true);
    }

    [Fact]
    public async Task ListAsyncApiOperations_SummarizesTheOperations()
    {
        // Act
        using var operations = await CallJsonAsync("list_asyncapi_operations", new() { ["document"] = DocumentName });

        // Assert
        var operation = operations.RootElement.EnumerateArray().Single();
        operation.GetProperty("id").GetString().ShouldBe("publishOrderPlaced");
        operation.GetProperty("action").GetString().ShouldBe("send");
        operation.GetProperty("address").GetString().ShouldBe("orders/placed");
        operation.GetProperty("summary").GetString().ShouldBe("Tells others an order was placed.");
        operation.GetProperty("messages")[0].GetProperty("name").GetString().ShouldBe("orderPlaced");
        operation.GetProperty("messages")[0].GetProperty("ref").GetString().ShouldBe("#/components/messages/orderPlaced");
    }

    [Fact]
    public async Task GetAsyncApiMessage_ResolvesItsSchemas()
    {
        // Act
        using var message = await CallJsonAsync("get_asyncapi_message", new() { ["document"] = DocumentName, ["message"] = "OrderPlaced" });

        // Assert
        var root = message.RootElement;
        root.GetProperty("name").GetString().ShouldBe("orderPlaced");
        root.GetProperty("pointer").GetString().ShouldBe("#/components/messages/orderPlaced");
        var reference = root.GetProperty("message").GetProperty("payload").GetProperty("$ref").GetString()!;
        reference.ShouldStartWith("#/components/schemas/");
        root.GetProperty("references").GetProperty(reference)
            .GetProperty("properties").TryGetProperty("orderId", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("nope", "orderPlaced", "No AsyncAPI document")]
    [InlineData(DocumentName, "nope", "orderPlaced")]
    [InlineData(DocumentName, null, "'message' is required")]
    public async Task GetAsyncApiMessage_UnknownTarget_IsAnError(string document, string? message, string expected)
    {
        // Act
        var result = await Client.CallToolAsync("get_asyncapi_message",
            new Dictionary<string, object?> { ["document"] = document, ["message"] = message });

        // Assert
        result.IsError.ShouldBe(true);
        Text(result).ShouldContain(expected);
    }

    [Fact]
    public async Task ListResourceTemplates_ListsTheApiDescriptionTemplate()
    {
        // Act
        var templates = await Client.ListResourceTemplatesAsync();

        // Assert
        templates.ShouldContain(template => template.UriTemplate == "apidescription://{kind}/{name}");
    }

    [Fact]
    public async Task ReadResource_ReturnsTheDocument()
    {
        // Act
        var result = await Client.ReadResourceAsync($"apidescription://asyncapi/{DocumentName}");

        // Assert
        var contents = result.Contents.ShouldHaveSingleItem().ShouldBeOfType<TextResourceContents>();
        contents.MimeType.ShouldBe("application/vnd.aai.asyncapi+json");
        using var document = JsonDocument.Parse(contents.Text);
        document.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("Orders");
    }

    [Fact]
    public async Task ReadResource_UnknownDocument_Fails()
    {
        // Act & Assert
        await Should.ThrowAsync<McpException>(() => Client.ReadResourceAsync("apidescription://asyncapi/nope").AsTask());
    }

    private async Task<JsonDocument> CallJsonAsync(string tool, Dictionary<string, object?> arguments)
    {
        var result = await Client.CallToolAsync(tool, arguments);
        result.IsError.ShouldNotBe(true, Text(result));
        return JsonDocument.Parse(Text(result));
    }

    private static string Text(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
