using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse.Tests;

/// <summary>
/// End-to-end example: builds a complete AsyncAPI document describing an SSE price-ticker endpoint
/// using all four SSE bindings (server, channel, operation, message) and serializes it to AsyncAPI v3 JSON.
/// </summary>
public class SseDocumentExampleTests
{
    private static AsyncApiDocument BuildTickerDocument()
    {
        var server = new AsyncApiServer
        {
            Host = "localhost:5001",
            Protocol = SseProtocol.ProtocolName,
            Bindings = new AsyncApiBindings<IServerBinding>
            {
                new SseServerBinding { Retry = 5000, Heartbeat = true },
            },
        };

        var priceMessage = new AsyncApiMessage
        {
            Name = "PriceUpdate",
            Bindings = new AsyncApiBindings<IMessageBinding>
            {
                new SseMessageBinding { Event = "price-update", Retry = 3000 },
            },
        };

        var channel = new AsyncApiChannel
        {
            Address = "/events",
            Messages = { ["priceUpdate"] = priceMessage },
            Bindings = new AsyncApiBindings<IChannelBinding>
            {
                new SseChannelBinding { Path = "/events", Method = SseProtocol.Methods.Get },
            },
        };

        var operation = new AsyncApiOperation
        {
            Action = AsyncApiAction.Receive,
            Channel = new AsyncApiChannelReference("#/channels/events"),
            Bindings = new AsyncApiBindings<IOperationBinding>
            {
                new SseOperationBinding { Direction = SseProtocol.Directions.ServerToClient },
            },
        };

        var document = new AsyncApiDocument
        {
            Info = new AsyncApiInfo { Title = "Price Ticker", Version = "1.0.0" },
        };
        document.Servers["sse"] = server;
        document.Channels["events"] = channel;
        document.Operations["onPriceUpdate"] = operation;
        return document;
    }

    [Fact]
    public void TickerDocument_SerializesWithSseBindings()
    {
        var document = BuildTickerDocument();

        using var stringWriter = new StringWriter();
        document.SerializeV3(new AsyncApiJsonWriter(stringWriter));
        stringWriter.Flush();
        var json = JsonNode.Parse(stringWriter.ToString())!;

        json["servers"]!["sse"]!["bindings"]!["sse"]!["retry"]!.GetValue<int>().ShouldBe(5000);
        json["channels"]!["events"]!["bindings"]!["sse"]!["path"]!.GetValue<string>().ShouldBe("/events");
        json["operations"]!["onPriceUpdate"]!["bindings"]!["sse"]!["direction"]!.GetValue<string>()
            .ShouldBe("serverToClient");
        json["channels"]!["events"]!["messages"]!["priceUpdate"]!["bindings"]!["sse"]!["event"]!
            .GetValue<string>().ShouldBe("price-update");
    }
}
