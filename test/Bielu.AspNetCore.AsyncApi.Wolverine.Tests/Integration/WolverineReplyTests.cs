using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures;
using ByteBard.AsyncAPI;
using Shouldly;
using Wolverine.Kafka;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Integration;

/// <summary>
/// <see cref="WolverineAsyncApiOptions.IncludeReplies"/> must document what a handler returns as the
/// reply of the listener's receive operation.
/// </summary>
public class WolverineReplyTests
{
    private const string ReceiveOperation = "receive.quotes.get-quote";
    private const string ReplyChannel = "receive.quotes.get-quote.reply";

    [Fact]
    public async Task TransformAsync_HandlerReturnsMessageAndRepliesIncluded_AddsReplyToReceiveOperation()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.IncludeReplies());

        // Assert
        var reply = document["operations"]![ReceiveOperation]!["reply"]!;
        reply["address"]!["location"]!.GetValue<string>().ShouldBe("$message.header#/reply-uri");
        reply["channel"]!["$ref"]!.GetValue<string>().ShouldBe($"#/channels/{ReplyChannel}");
        reply["messages"]!.AsArray().Single()!["$ref"]!.GetValue<string>()
            .ShouldBe($"#/channels/{ReplyChannel}/messages/quotes.quote");
    }

    [Fact]
    public async Task TransformAsync_HandlerReturnsMessageAndRepliesIncluded_AddsAddresslessReplyChannelWithReplyMessage()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.IncludeReplies());

        // Assert
        var channel = document["channels"]![ReplyChannel]!.AsObject();
        channel["address"].ShouldBeNull();
        channel["messages"]!["quotes.quote"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/messages/quotes.quote");

        var message = document["components"]!["messages"]!["quotes.quote"]!;
        message["payload"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/quote");
        message["summary"]!.GetValue<string>().ShouldBe("The price quoted for a product.");
    }

    [Fact]
    public async Task TransformAsync_HandlerReturnsNothingAndRepliesIncluded_LeavesReceiveOperationWithoutReply()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.IncludeReplies());

        // Assert
        document["operations"]!["receive.stock.stock-changed"]!.AsObject().ContainsKey("reply").ShouldBeFalse();
        document["channels"]!.AsObject().ContainsKey("receive.stock.stock-changed.reply").ShouldBeFalse();
    }

    [Fact]
    public async Task TransformAsync_RepliesNotIncluded_LeavesReceiveOperationWithoutReply()
    {
        // Act
        var document = await GetDocumentAsync(_ => { });

        // Assert
        document["operations"]![ReceiveOperation]!.AsObject().ContainsKey("reply").ShouldBeFalse();
        document["channels"]!.AsObject().ContainsKey(ReplyChannel).ShouldBeFalse();
        document["components"]!["messages"]!.AsObject().ContainsKey("quotes.quote").ShouldBeFalse();
    }

    [Fact]
    public async Task TransformAsync_AsyncApi2DocumentAndRepliesIncluded_AddsNoReplyChannel()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.IncludeReplies(), AsyncApiVersion.AsyncApi2_0);

        // Assert
        document["channels"]!.AsObject().Select(c => c.Key).ShouldBe(["orders", "quotes", "stock"], ignoreOrder: true);
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
        Action<WolverineAsyncApiOptions> configure,
        AsyncApiVersion version = AsyncApiVersion.AsyncApi3_0) =>
        WolverineTestApp.GetDocumentAsync("messaging", options =>
            {
                options.AsyncApiVersion = version;
                options.AddServer("kafka", "localhost:9092", "kafka");
                options.AddWolverine(wolverine => configure(wolverine.IncludeSchemes("kafka")));
            },
            configureWolverine: opts => opts.ListenToKafkaTopic("quotes").DefaultIncomingMessage<GetQuote>());
}
