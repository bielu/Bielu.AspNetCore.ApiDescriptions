using System.Net;
using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

public sealed class LikeC4CommandWorkerTests : IDisposable
{
    private const string Orders = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "Orders", "version": "1" },
          "channels": { "c": { "address": "orders.created", "messages": { "m": { "name": "OrderCreated" } } } },
          "operations": { "s": { "action": "send", "channel": { "$ref": "#/channels/c" } } }
        }
        """;

    private const string Inventory = """
        asyncapi: 3.0.0
        info: { title: Inventory, version: '1' }
        channels:
          c: { address: orders.created, messages: { m: { name: OrderCreated } } }
        operations:
          r: { action: receive, channel: { $ref: '#/channels/c' } }
        """;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"asyncapi_cli_likec4_{Guid.NewGuid():N}");
    private readonly List<string> _info = [];
    private readonly List<string> _errors = [];

    public LikeC4CommandWorkerTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "docs"));
        File.WriteAllText(Path.Combine(_directory, "docs", "orders.asyncapi.json"), Orders);
        File.WriteAllText(Path.Combine(_directory, "docs", "inventory.asyncapi.yaml"), Inventory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string Output => Path.Combine(_directory, "architecture", "asyncapi.c4");

    private int Run(LikeC4CommandContext context, HttpMessageHandler? handler = null)
    {
        context.OutputPath = context.OutputPath.Length > 0 ? context.OutputPath : Output;
        return new LikeC4CommandWorker(context, _info.Add, _errors.Add, handler).Process();
    }

    [Fact]
    public void Process_GlobbedSources_WritesTheModelOfEveryDocument()
    {
        // Arrange
        var context = new LikeC4CommandContext();
        context.Sources.Add(Path.Combine(_directory, "docs", "*.asyncapi.*"));

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(0, string.Join('\n', _errors));
        var model = File.ReadAllText(Output);
        model.ShouldContain("orders = asyncapiService 'Orders'");
        model.ShouldContain("inventory = asyncapiService 'Inventory'");
        model.ShouldContain("orders -[asyncapiFlow]-> inventory 'OrderCreated'");
        model.ShouldContain("link ../docs/orders.asyncapi.json 'AsyncAPI document'");
        _info.ShouldContain(line => line.Contains("2 AsyncAPI and 0 Arazzo document(s)"));
    }

    [Fact]
    public void Process_ArazzoDocument_AddsADynamicViewPerWorkflow()
    {
        // Arrange
        var path = Path.Combine(_directory, "docs", "checkout.arazzo.yaml");
        File.WriteAllText(path, """
            arazzo: 1.1.0
            info: { title: Checkout, version: '1' }
            sourceDescriptions:
              - { name: inventory, url: ./inventory.asyncapi.yaml, type: asyncapi }
            workflows:
              - workflowId: placeOrder
                steps:
                  - { stepId: announce, operationId: r }
            """);
        var context = new LikeC4CommandContext();
        context.Sources.Add(Path.Combine(_directory, "docs", "*.asyncapi.*"));
        context.Workflows.Add(path);

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(0, string.Join('\n', _errors));
        var model = File.ReadAllText(Output);
        model.ShouldContain("dynamic view arazzoPlaceOrder {");
        model.ShouldContain("client -> ordersCreated 'sends OrderCreated'");
        _info.ShouldContain(line => line.Contains("2 AsyncAPI and 1 Arazzo document(s)"));
    }

    [Fact]
    public void Process_UnreadableArazzoDocument_ReportsIt()
    {
        // Arrange
        var context = new LikeC4CommandContext();
        context.Workflows.Add(Path.Combine(_directory, "docs", "missing.arazzo.json"));

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        _errors.ShouldHaveSingleItem().ShouldContain("missing.arazzo.json");
    }

    [Fact]
    public void Process_NamedSourceAndLink_UsesThem()
    {
        // Arrange
        var context = new LikeC4CommandContext();
        context.Sources.Add("shop-orders=" + Path.Combine(_directory, "docs", "orders.asyncapi.json"));
        context.Links.Add("shop-orders=https://orders.example.com/asyncapi.json");

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(0, string.Join('\n', _errors));
        var model = File.ReadAllText(Output);
        model.ShouldContain("shopOrders = asyncapiService 'Orders'");
        model.ShouldContain("asyncapi 'shop-orders'");
        model.ShouldContain("link https://orders.example.com/asyncapi.json");
    }

    [Fact]
    public void Process_UrlSource_IsFetchedAndLinkedToItself()
    {
        // Arrange
        var context = new LikeC4CommandContext();
        context.Sources.Add("https://orders.example.com/asyncapi/orders.json?v=1");

        // Act
        var exitCode = Run(context, new StubHandler(HttpStatusCode.OK, Orders));

        // Assert
        exitCode.ShouldBe(0, string.Join('\n', _errors));
        var model = File.ReadAllText(Output);
        model.ShouldContain("orders = asyncapiService 'Orders'");
        model.ShouldContain("link https://orders.example.com/asyncapi/orders.json?v=1");
    }

    [Fact]
    public void Process_UrlThatFails_ReportsIt()
    {
        // Arrange
        var context = new LikeC4CommandContext();
        context.Sources.Add("orders=https://orders.example.com/asyncapi.json");

        // Act
        var exitCode = Run(context, new StubHandler(HttpStatusCode.NotFound, string.Empty));

        // Assert
        exitCode.ShouldBe(1);
        _errors.ShouldHaveSingleItem().ShouldStartWith("Could not read 'https://orders.example.com/asyncapi.json'");
        File.Exists(Output).ShouldBeFalse();
    }

    [Fact]
    public void Process_MissingFile_ReportsIt()
    {
        // Arrange
        var context = new LikeC4CommandContext();
        context.Sources.Add(Path.Combine(_directory, "docs", "missing.json"));

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        _errors.ShouldHaveSingleItem().ShouldContain("missing.json");
    }

    [Fact]
    public void Process_GlobMatchingNothing_ReportsNoDocuments()
    {
        // Arrange
        var context = new LikeC4CommandContext();
        context.Sources.Add(Path.Combine(_directory, "docs", "*.nothing"));

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        _errors.ShouldBe(["No AsyncAPI or Arazzo documents found."]);
    }

    [Fact]
    public void Process_DocumentThatIsNotAsyncApi_ReportsIt()
    {
        // Arrange
        var path = Path.Combine(_directory, "docs", "openapi.json");
        File.WriteAllText(path, """{ "openapi": "3.1.0" }""");
        var context = new LikeC4CommandContext();
        context.Sources.Add(path);

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        _errors.ShouldHaveSingleItem().ShouldStartWith("AsyncAPI document 'openapi':");
    }

    [Fact]
    public void Process_TwoSourcesWithTheSameName_ReportsIt()
    {
        // Arrange
        var context = new LikeC4CommandContext();
        context.Sources.Add("orders=" + Path.Combine(_directory, "docs", "orders.asyncapi.json"));
        context.Sources.Add("orders=" + Path.Combine(_directory, "docs", "inventory.asyncapi.yaml"));

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        _errors.ShouldHaveSingleItem().ShouldContain("'orders'");
    }

    [Theory]
    [InlineData("orders")]
    [InlineData("=https://x")]
    [InlineData("orders=")]
    public void Process_MalformedLink_ReportsIt(string link)
    {
        // Arrange
        var context = new LikeC4CommandContext();
        context.Sources.Add(Path.Combine(_directory, "docs", "orders.asyncapi.json"));
        context.Links.Add(link);

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        _errors.ShouldHaveSingleItem().ShouldStartWith("--link must be name=<url>");
    }

    [Theory]
    [InlineData("obj/orders.asyncapi.json", "orders")]
    [InlineData("orders.yaml", "orders")]
    [InlineData("orders.v1.yml", "orders.v1")]
    [InlineData("checkout.arazzo.yaml", "checkout")]
    [InlineData("https://x.example.com/docs/inventory.json?raw=1", "inventory")]
    [InlineData("https://x.example.com/", "https://x.example.com/")]
    [InlineData(".json", ".json")]
    public void NameOf_TrimsDocumentSuffixes(string location, string name)
    {
        // Act & Assert
        LikeC4CommandWorker.NameOf(location).ShouldBe(name);
    }

    [Fact]
    public void Constructor_ThrowsForNullArguments()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new LikeC4CommandWorker(null!, _ => { }, _ => { }));
        Should.Throw<ArgumentNullException>(() => new LikeC4CommandWorker(new LikeC4CommandContext(), null!, _ => { }));
        Should.Throw<ArgumentNullException>(() => new LikeC4CommandWorker(new LikeC4CommandContext(), _ => { }, null!));
    }

    private sealed class StubHandler(HttpStatusCode status, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(content) });
    }
}
