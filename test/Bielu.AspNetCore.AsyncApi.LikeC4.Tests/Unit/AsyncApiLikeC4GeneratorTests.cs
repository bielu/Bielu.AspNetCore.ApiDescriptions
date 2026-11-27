using Bielu.AspNetCore.AsyncApi.LikeC4.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.LikeC4.Tests.Unit;

public class AsyncApiLikeC4GeneratorTests
{
    private static AsyncApiLikeC4Source Source(string name, string json) => new(name, json);

    private static string Document(string channels = "{}", string operations = "{}", string servers = "{}") =>
        $$"""
          { "asyncapi": "3.0.0", "info": { "title": "T", "version": "1" }, "servers": {{servers}}, "channels": {{channels}}, "operations": {{operations}} }
          """;

    [Fact]
    public void Generate_ShopDocuments_MatchesGoldenModel()
    {
        // Arrange
        var sources = ShopFixtures.Sources();

        // Act
        var model = AsyncApiLikeC4Generator.Generate(sources);

        // Assert
        ShopFixtures.ShouldMatchGolden(model, "shop.c4");
    }

    [Fact]
    public void Generate_ThrowsForNullSources()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => AsyncApiLikeC4Generator.Generate(null!));
    }

    [Fact]
    public void Generate_ThrowsForNoSources()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => AsyncApiLikeC4Generator.Generate([]))
            .Message.ShouldContain("At least one");
    }

    [Fact]
    public void Generate_ThrowsForDuplicateNames()
    {
        // Arrange
        var sources = new[] { Source("orders", Document()), Source("Orders", Document()) };

        // Act & Assert
        Should.Throw<ArgumentException>(() => AsyncApiLikeC4Generator.Generate(sources))
            .Message.ShouldContain("'orders'");
    }

    [Theory]
    [InlineData("{ \"openapi\": \"3.1.0\" }", "no 'asyncapi' field")]
    [InlineData("[1, 2]", "no 'asyncapi' field")]
    [InlineData("{ not json", "neither JSON nor YAML")]
    [InlineData("a: [unclosed", "neither JSON nor YAML")]
    public void Generate_ThrowsForContentThatIsNotAnAsyncApiDocument(string content, string reason)
    {
        // Act
        var exception = Should.Throw<FormatException>(() => AsyncApiLikeC4Generator.Generate([Source("orders", content)]));

        // Assert
        exception.Message.ShouldStartWith("AsyncAPI document 'orders':");
        exception.Message.ShouldContain(reason);
    }

    [Fact]
    public void Generate_ChannelWithoutAddress_IsNotSharedWithOtherDocuments()
    {
        // Arrange
        var channels = """{ "events": { "messages": {} } }""";
        var sources = new[] { Source("a", Document(channels)), Source("b", Document(channels)) };

        // Act
        var model = AsyncApiLikeC4Generator.Generate(sources);

        // Assert
        model.ShouldContain("events = asyncapiChannel 'events'");
        model.ShouldContain("events2 = asyncapiChannel 'events'");
    }

    [Fact]
    public void Generate_SenderAndReceiverWithNoMessageInCommon_HaveNoFlow()
    {
        // Arrange
        var sender = Document(
            """{ "c": { "address": "x", "messages": { "a": { "name": "A" } } } }""",
            """{ "s": { "action": "send", "channel": { "$ref": "#/channels/c" } } }""");
        var receiver = Document(
            """{ "c": { "address": "x", "messages": { "b": { "name": "B" } } } }""",
            """{ "r": { "action": "receive", "channel": { "$ref": "#/channels/c" } } }""");

        // Act
        var model = AsyncApiLikeC4Generator.Generate([Source("sender", sender), Source("receiver", receiver)]);

        // Assert
        model.ShouldContain("sender -[asyncapiSend]-> x 'sends A'");
        model.ShouldContain("x -[asyncapiReceive]-> receiver 'delivers B'");
        model.ShouldNotContain("-[asyncapiFlow]->");
    }

    [Fact]
    public void Generate_ServiceReceivingWhatItSends_HasNoFlowToItself()
    {
        // Arrange
        var document = Document(
            """{ "c": { "address": "loop", "messages": { "m": { "name": "M" } } } }""",
            """
            {
              "s": { "action": "send", "channel": { "$ref": "#/channels/c" } },
              "r": { "action": "receive", "channel": { "$ref": "#/channels/c" } }
            }
            """);

        // Act
        var model = AsyncApiLikeC4Generator.Generate([Source("self", document)]);

        // Assert
        model.ShouldNotContain("-[asyncapiFlow]->");
    }

    [Fact]
    public void Generate_SenderOrReceiverNamingNoMessages_FlowCarriesTheOthersMessages()
    {
        // Arrange
        var sender = Document(
            """{ "c": { "address": "x", "title": "X channel" } }""",
            """{ "s": { "action": "send", "channel": { "$ref": "#/channels/c" } } }""");
        var receiver = Document(
            """{ "c": { "address": "x", "messages": { "b": { "name": "B" } } } }""",
            """{ "r": { "action": "receive", "channel": { "$ref": "#/channels/c" } } }""");
        var silent = Document(
            """{ "c": { "address": "x" } }""",
            """{ "r": { "action": "receive", "channel": { "$ref": "#/channels/c" } } }""");

        // Act
        var model = AsyncApiLikeC4Generator.Generate([Source("sender", sender), Source("receiver", receiver), Source("silent", silent)]);

        // Assert
        model.ShouldContain("sender -[asyncapiFlow]-> receiver 'B'");
        model.ShouldContain("sender -[asyncapiFlow]-> silent 'X channel'");
        model.ShouldContain("Through X channel.");
    }

    [Fact]
    public void Generate_SameFlowTwice_IsWrittenOnce()
    {
        // Arrange
        var sender = Document(
            """{ "c": { "address": "x", "messages": { "m": { "name": "M" } } } }""",
            """
            {
              "s1": { "action": "send", "channel": { "$ref": "#/channels/c" } },
              "s2": { "action": "send", "channel": { "$ref": "#/channels/c" } }
            }
            """);
        var receiver = Document(
            """{ "c": { "address": "x", "messages": { "m": { "name": "M" } } } }""",
            """{ "r": { "action": "receive", "channel": { "$ref": "#/channels/c" } } }""");

        // Act
        var model = AsyncApiLikeC4Generator.Generate([Source("sender", sender), Source("receiver", receiver)]);

        // Assert
        model.Split("-[asyncapiFlow]->").Length.ShouldBe(2);
        model.Split("-[asyncapiSend]->").Length.ShouldBe(3);
    }

    [Theory]
    [InlineData("model", "model2")]
    [InlineData("views", "views2")]
    [InlineData("1st-service", "service1stService")]
    [InlineData("***", "service")]
    [InlineData("orders api", "ordersApi")]
    public void Generate_ServiceName_IsMadeAValidIdentifier(string name, string id)
    {
        // Act
        var model = AsyncApiLikeC4Generator.Generate([Source(name, Document())]);

        // Assert
        model.ShouldContain($"  {id} = asyncapiService 'T' {{");
    }

    [Fact]
    public void Generate_EscapesQuotesAndBackslashes()
    {
        // Arrange
        var document = """
            { "asyncapi": "3.0.0", "info": { "title": "It's C:\\shop", "version": "1", "description": "Line 1\r\nIt's line 2" } }
            """;

        // Act
        var model = AsyncApiLikeC4Generator.Generate([Source("shop", document)]);

        // Assert
        model.ShouldContain("shop = asyncapiService 'It\\'s C:/shop' {");
        model.ShouldContain("      Line 1\n      It\\'s line 2\n");
    }

    [Fact]
    public void Generate_LinkWithWhitespace_IsLeftOut()
    {
        // Arrange
        var source = new AsyncApiLikeC4Source("shop", Document()) { Link = "docs/shop api.json" };

        // Act
        var model = AsyncApiLikeC4Generator.Generate([source]);

        // Assert
        model.ShouldNotContain("link ");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Source_ThrowsForMissingName(string? name)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new AsyncApiLikeC4Source(name!, "{}"));
    }

    [Fact]
    public void Source_ThrowsForNullDocument()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new AsyncApiLikeC4Source("shop", null!));
    }

    [Fact]
    public void Generate_SeveralOperationsOnOneChannel_FlowCarriesEveryMessage()
    {
        // Arrange
        var sender = Document(
            """{ "c": { "address": "x", "messages": { "created": { "name": "Created" }, "cancelled": { "name": "Cancelled" } } } }""",
            """
            {
              "s1": { "action": "send", "channel": { "$ref": "#/channels/c" }, "messages": [{ "$ref": "#/channels/c/messages/created" }] },
              "s2": { "action": "send", "channel": { "$ref": "#/channels/c" }, "messages": [{ "$ref": "#/channels/c/messages/cancelled" }] }
            }
            """);
        var receiver = Document(
            """{ "c": { "address": "x", "messages": { "created": { "name": "Created" }, "cancelled": { "name": "Cancelled" } } } }""",
            """{ "r": { "action": "receive", "channel": { "$ref": "#/channels/c" } } }""");

        // Act
        var model = AsyncApiLikeC4Generator.Generate([Source("sender", sender), Source("receiver", receiver)]);

        // Assert
        model.Split("-[asyncapiFlow]->").Length.ShouldBe(2);
        model.ShouldContain("sender -[asyncapiFlow]-> receiver 'Created, Cancelled'");
    }
}
