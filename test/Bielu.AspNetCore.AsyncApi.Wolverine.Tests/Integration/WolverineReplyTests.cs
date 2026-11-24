using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures;
using ByteBard.AsyncAPI;
using Shouldly;
using Wolverine;
using Wolverine.Kafka;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Integration;

/// <summary>
/// What a handler returns must be documented as the reply of the listener's receive operation: always
/// when the returned message has no route, and with
/// <see cref="WolverineAsyncApiOptions.IncludeReplies"/> when it has one.
/// </summary>
public class WolverineReplyTests
{
    private const string QuoteOperation = "receive.quotes.get-quote";
    private const string QuoteReplyChannel = "receive.quotes.get-quote.reply";
    private const string TrackOperation = "receive.orders.track-order";
    private const string TrackReplyChannel = "receive.orders.track-order.reply";

    [Fact]
    public async Task TransformAsync_HandlerReturnsUnroutedMessage_AddsReplyToReceiveOperation()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var reply = document["operations"]![QuoteOperation]!["reply"]!;
        reply["address"]!["location"]!.GetValue<string>().ShouldBe("$message.header#/reply-uri");
        reply["channel"]!["$ref"]!.GetValue<string>().ShouldBe($"#/channels/{QuoteReplyChannel}");
        reply["messages"]!.AsArray().Single()!["$ref"]!.GetValue<string>()
            .ShouldBe($"#/channels/{QuoteReplyChannel}/messages/quotes.quote");
    }

    [Fact]
    public async Task TransformAsync_HandlerReturnsUnroutedMessage_AddsAddresslessReplyChannelWithReplyMessage()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var channel = document["channels"]![QuoteReplyChannel]!.AsObject();
        channel["address"].ShouldBeNull();
        channel["messages"]!["quotes.quote"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/messages/quotes.quote");

        var message = document["components"]!["messages"]!["quotes.quote"]!;
        message["payload"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/quote");
        message["summary"]!.GetValue<string>().ShouldBe("The price quoted for a product.");
    }

    [Theory]
    [InlineData("receive.quotes.list-quotes")]
    [InlineData("receive.quotes.find-quotes")]
    public async Task TransformAsync_HandlerReturnsCollectionOfMessages_AddsReplyWithElementMessage(string operationId)
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var reply = document["operations"]![operationId]!["reply"]!;
        reply["channel"]!["$ref"]!.GetValue<string>().ShouldBe($"#/channels/{operationId}.reply");
        reply["messages"]!.AsArray().Single()!["$ref"]!.GetValue<string>()
            .ShouldBe($"#/channels/{operationId}.reply/messages/quotes.quote");
    }

    [Fact]
    public async Task TransformAsync_HandlerReturnsRoutedMessage_LeavesReceiveOperationWithoutReply()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        document["operations"]![TrackOperation]!.AsObject().ContainsKey("reply").ShouldBeFalse();
        document["channels"]!.AsObject().ContainsKey(TrackReplyChannel).ShouldBeFalse();
    }

    [Fact]
    public async Task TransformAsync_HandlerReturnsRoutedMessageAndRepliesIncluded_AddsReplyToReceiveOperation()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.IncludeReplies());

        // Assert
        var reply = document["operations"]![TrackOperation]!["reply"]!;
        reply["channel"]!["$ref"]!.GetValue<string>().ShouldBe($"#/channels/{TrackReplyChannel}");
        reply["messages"]!.AsArray().Single()!["$ref"]!.GetValue<string>()
            .ShouldBe($"#/channels/{TrackReplyChannel}/messages/orders.order-placed");
        document["channels"]![TrackReplyChannel]!["messages"]!["orders.order-placed"]!["$ref"]!.GetValue<string>()
            .ShouldBe("#/components/messages/orders.order-placed");
    }

    [Fact]
    public async Task TransformAsync_HandlerReturnsNothing_LeavesReceiveOperationWithoutReply()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.IncludeReplies());

        // Assert
        document["operations"]!["receive.stock.stock-changed"]!.AsObject().ContainsKey("reply").ShouldBeFalse();
        document["channels"]!.AsObject().ContainsKey("receive.stock.stock-changed.reply").ShouldBeFalse();
    }

    [Fact]
    public async Task TransformAsync_AsyncApi2Document_AddsNoReplyChannel()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.IncludeReplies(), AsyncApiVersion.AsyncApi2_0);

        // Assert
        document["channels"]!.AsObject().Select(c => c.Key).ShouldBe(["orders", "quote-lists", "quote-searches", "quotes", "stock", "tracking"], ignoreOrder: true);
        document["components"]!["messages"]!.AsObject().ContainsKey("quotes.quote").ShouldBeFalse();
    }

    [Theory]
    [InlineData("retail-prices", "pricing.retail-price")]
    [InlineData("wholesale-prices", "pricing.wholesale-price")]
    public async Task TransformAsync_HandlersBoundToDifferentListeners_AddsEachListenersOwnReply(string channel, string replyMessage)
    {
        // Act
        var document = await GetDocumentAsync(configureWolverine: opts =>
        {
            opts.Discovery.IncludeType(typeof(RetailPriceDesk)).IncludeType(typeof(WholesalePriceDesk));
            opts.ListenToKafkaTopic("retail-prices").DefaultIncomingMessage<PriceRequest>().AddStickyHandler(typeof(RetailPriceDesk));
            opts.ListenToKafkaTopic("wholesale-prices").DefaultIncomingMessage<PriceRequest>().AddStickyHandler(typeof(WholesalePriceDesk));
        });

        // Assert
        var (operationId, operation) = ReceiveOperation(document, channel);
        var reply = operation["reply"]!;
        reply["channel"]!["$ref"]!.GetValue<string>().ShouldBe($"#/channels/{operationId}.reply");
        reply["messages"]!.AsArray().Single()!["$ref"]!.GetValue<string>()
            .ShouldBe($"#/channels/{operationId}.reply/messages/{replyMessage}");
    }

    [Fact]
    public async Task TransformAsync_ListenerRelaysToLocalHandlers_LeavesReceiveOperationWithoutReply()
    {
        // Act
        var document = await GetDocumentAsync(configureWolverine: opts =>
        {
            opts.Discovery.IncludeType(typeof(StockAuditor)).IncludeType(typeof(PriceAuditor));
            opts.LocalQueue("stock-audits").AddStickyHandler(typeof(StockAuditor));
            opts.LocalQueue("price-audits").AddStickyHandler(typeof(PriceAuditor));
            opts.ListenToKafkaTopic("audits").DefaultIncomingMessage<AuditRequest>();
        });

        // Assert
        var (_, operation) = ReceiveOperation(document, "audits");
        operation.ContainsKey("reply").ShouldBeFalse();
        document["components"]!["messages"]!.AsObject().ContainsKey("audit.audit-result").ShouldBeFalse();
    }

    [Fact]
    public void IncludeReplies_ReturnsSameOptions()
    {
        // Arrange
        var options = new WolverineAsyncApiOptions();

        // Act
        var result = options.IncludeReplies();

        // Assert
        result.ShouldBeSameAs(options);
        options.RepliesIncluded.ShouldBeTrue();
    }

    /// <summary>The receive operation on the channel whose address is <paramref name="address"/>.</summary>
    private static (string Id, JsonObject Operation) ReceiveOperation(JsonNode document, string address)
    {
        var channelKey = document["channels"]!.AsObject()
            .Single(c => c.Value!["address"]?.GetValue<string>() == address).Key;
        var operation = document["operations"]!.AsObject().Single(o =>
            o.Value!["action"]!.GetValue<string>() == "receive"
            && o.Value["channel"]!["$ref"]!.GetValue<string>() == $"#/channels/{channelKey}");
        return (operation.Key, operation.Value!.AsObject());
    }

    private static Task<JsonNode> GetDocumentAsync(
        Action<WolverineAsyncApiOptions>? configure = null,
        AsyncApiVersion version = AsyncApiVersion.AsyncApi3_0,
        Action<WolverineOptions>? configureWolverine = null) =>
        WolverineTestApp.GetDocumentAsync("messaging", options =>
            {
                options.AsyncApiVersion = version;
                options.AddServer("kafka", "localhost:9092", "kafka");
                options.AddWolverine(wolverine => configure?.Invoke(wolverine.IncludeSchemes("kafka")));
            },
            configureWolverine: opts =>
            {
                opts.ListenToKafkaTopic("quotes").DefaultIncomingMessage<GetQuote>();
                opts.ListenToKafkaTopic("tracking").DefaultIncomingMessage<TrackOrder>();
                opts.ListenToKafkaTopic("quote-lists").DefaultIncomingMessage<ListQuotes>();
                opts.ListenToKafkaTopic("quote-searches").DefaultIncomingMessage<FindQuotes>();
                configureWolverine?.Invoke(opts);
            });
}
