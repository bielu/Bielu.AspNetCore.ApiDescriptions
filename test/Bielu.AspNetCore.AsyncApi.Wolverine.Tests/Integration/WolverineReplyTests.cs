using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures;
using ByteBard.AsyncAPI;
using Shouldly;
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
        document["channels"]!.AsObject().Select(c => c.Key).ShouldBe(["orders", "quotes", "stock", "tracking"], ignoreOrder: true);
        document["components"]!["messages"]!.AsObject().ContainsKey("quotes.quote").ShouldBeFalse();
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

    private static Task<JsonNode> GetDocumentAsync(
        Action<WolverineAsyncApiOptions>? configure = null,
        AsyncApiVersion version = AsyncApiVersion.AsyncApi3_0) =>
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
            });
}
