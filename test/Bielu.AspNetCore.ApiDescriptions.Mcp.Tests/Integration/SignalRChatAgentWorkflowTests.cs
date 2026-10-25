using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp.Tests.Integration;

/// <summary>
/// The workflow the MCP tools exist for, done the way an agent would: connect to the SignalRChat example's MCP server
/// over its HTTP transport, find the operations to send and receive a chat message, read their contracts, and build a
/// working SignalR client from what the contracts say, never from the example's source. Hub path, method names,
/// transport and payload all come from the tool results.
/// </summary>
public sealed class SignalRChatAgentWorkflowTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private McpClient? _client;

    public SignalRChatAgentWorkflowTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private McpClient Client => _client!;

    public async Task InitializeAsync()
    {
        var http = _factory.CreateClient();
        _client = await McpClient.CreateAsync(new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            http,
            loggerFactory: null,
            ownsHttpClient: true));
    }

    public async Task DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }
    }

    [Fact]
    public async Task ApiCatalog_ListsTheMcpServerOnceAlongsideTheDocuments()
    {
        // Act: where an agent that knows only the app's address starts.
        var catalog = JsonNode.Parse(await _factory.CreateClient().GetStringAsync("/.well-known/api-catalog"))!;

        // Assert
        var linkset = catalog["linkset"]!.AsArray();
        var items = linkset[0]!["item"]!.AsArray();
        var mcp = items.Where(item => item!["title"]?.GetValue<string>() == "MCP server").ShouldHaveSingleItem()!;
        mcp["href"]!.GetValue<string>().ShouldEndWith("/mcp");
        linkset[1]!["service-desc"]!.AsArray().Select(link => link!["href"]!.GetValue<string>())
            .ShouldContain(href => href.EndsWith("/asyncapi/signalr.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HttpTransport_ListsTheExampleDocuments()
    {
        // Act
        var result = await Client.CallToolAsync("list_api_descriptions", new Dictionary<string, object?>());

        // Assert
        var documents = Structured(result)["documents"]!.AsArray();
        documents.Select(document => document!["name"]!.GetValue<string>()).ShouldBe(["signalr", "signalr-secure"], ignoreOrder: true);
        documents.ShouldAllBe(document => document!["specificationVersion"]!.GetValue<string>() == "3.1.0");
        documents.Single(document => document!["name"]!.GetValue<string>() == "signalr")!["title"]!.GetValue<string>().ShouldBe("SignalR Chat");
    }

    [Fact]
    public async Task Agent_BuildsAWorkingChatClientFromTheContractAlone()
    {
        // Arrange: find what the app receives (what a client sends) to post a message, and what it sends back.
        var send = await FindOneAsync("SendMessage", action: "receive");
        var receive = await FindOneAsync("ReceiveMessage", action: "send");
        var sendContract = await GetOperationAsync(send);
        var receiveContract = await GetOperationAsync(receive);

        // The hub, transport and method names, as the bindings document them.
        var channel = Reference(sendContract, sendContract["channel"]!["pointer"]!.GetValue<string>());
        var hub = channel["bindings"]!["signalr"]!["hub"]!.GetValue<string>();
        channel["bindings"]!["signalr"]!["transports"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldContain("longPolling");
        var invoke = sendContract["operation"]!["bindings"]!["signalr"]!;
        invoke["direction"]!.GetValue<string>().ShouldBe("clientToServer");
        invoke["callType"]!.GetValue<string>().ShouldBe("send");
        var target = invoke["target"]!.GetValue<string>();
        var push = receiveContract["operation"]!["bindings"]!["signalr"]!;
        push["direction"]!.GetValue<string>().ShouldBe("serverToClient");
        var handler = push["target"]!.GetValue<string>();

        // A payload that satisfies the documented schema of the message to send.
        var message = sendContract["messages"]!.AsArray().ShouldHaveSingleItem()!;
        var schema = Resolve(sendContract, message["payload"]!.AsObject());
        var payload = Example(schema);

        // The app receives on the same channel the client listens on.
        receiveContract["channel"]!["pointer"]!.GetValue<string>().ShouldBe(sendContract["channel"]!["pointer"]!.GetValue<string>());

        var server = _factory.Server;
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, hub), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();
        var arrived = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>(handler, received => arrived.TrySetResult(received));

        // Act
        await connection.StartAsync();
        await connection.SendAsync(target, payload);
        var received = await arrived.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // Assert: the documented receive handler got the message the documented method was sent.
        received.GetProperty("text").GetString().ShouldBe(payload["text"]!.GetValue<string>());
        received.GetProperty("user").GetString().ShouldBe(payload["user"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetApiOperation_SaysWhatTheContractDoesNotSpecify()
    {
        // Arrange: GetOnlineUsers returns a value, which the document does not describe.
        var handle = await FindOneAsync("GetOnlineUsers", action: "receive");

        // Act
        var contract = await GetOperationAsync(handle);

        // Assert
        var fields = contract["notSpecified"]!.AsArray().Select(entry => entry!["field"]!.GetValue<string>()).ToList();
        fields.ShouldContain("reply");
        fields.ShouldContain("messages");
        contract["operation"]!["bindings"]!["signalr"]!["callType"]!.GetValue<string>().ShouldBe("invocation");
    }

    [Fact]
    public async Task GetApiOperation_SecuredHub_ReturnsTheSecurityScheme()
    {
        // Arrange
        var handle = await FindOneAsync("SendMessage", action: "receive", document: "signalr-secure");

        // Act
        var contract = await GetOperationAsync(handle);

        // Assert
        var requirement = contract["security"]!.AsArray().ShouldHaveSingleItem()!;
        requirement["type"]!.GetValue<string>().ShouldBe("httpApiKey");
        var scheme = Reference(contract, requirement["scheme"]!.GetValue<string>());
        scheme["in"]!.GetValue<string>().ShouldBe("query");
        scheme["name"]!.GetValue<string>().ShouldBe("api_key");
    }

    private async Task<JsonObject> FindOneAsync(string query, string action, string document = "signalr")
    {
        var result = await Client.CallToolAsync("find_api_operations",
            new Dictionary<string, object?> { ["query"] = query, ["action"] = action, ["document"] = document, ["protocol"] = "signalr" });
        return Structured(result)["matches"]!.AsArray().First()!["handle"]!.AsObject();
    }

    private async Task<JsonObject> GetOperationAsync(JsonObject handle)
    {
        var result = await Client.CallToolAsync("get_api_operation",
            handle.ToDictionary(entry => entry.Key, entry => (object?)entry.Value!.GetValue<string>()));
        var contract = Structured(result);
        contract["nextCursor"].ShouldBeNull("the example's operations fit in one response");
        return contract;
    }

    /// <summary>A part of the document the contract refers to, looked up by its reference as written.</summary>
    private static JsonObject Reference(JsonObject contract, string pointer) => contract["references"]![pointer]!.AsObject();

    /// <summary>The schema itself when it is a <c>$ref</c>.</summary>
    private static JsonObject Resolve(JsonObject contract, JsonObject schema) =>
        schema["$ref"] is { } reference ? Resolve(contract, Reference(contract, reference.GetValue<string>())) : schema;

    /// <summary>An instance of an object schema with each required property given a value of its documented type.</summary>
    private static JsonObject Example(JsonObject schema)
    {
        var instance = new JsonObject();
        foreach (var name in schema["required"]!.AsArray().Select(required => required!.GetValue<string>()))
        {
            var property = schema["properties"]![name]!;
            instance[name] = (property["type"]?.GetValue<string>(), property["format"]?.GetValue<string>()) switch
            {
                ("string", "date-time") => DateTimeOffset.UtcNow.ToString("O"),
                ("string", _) => $"{name}-{Guid.NewGuid():N}",
                ("integer", _) => 1,
                ("boolean", _) => true,
                var other => throw new InvalidOperationException($"No example for {other}."),
            };
        }

        return instance;
    }

    private static JsonObject Structured(CallToolResult result)
    {
        result.IsError.ShouldNotBe(true, string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text)));
        result.StructuredContent.ShouldNotBeNull();
        return JsonNode.Parse(result.StructuredContent.Value.GetRawText())!.AsObject();
    }
}
