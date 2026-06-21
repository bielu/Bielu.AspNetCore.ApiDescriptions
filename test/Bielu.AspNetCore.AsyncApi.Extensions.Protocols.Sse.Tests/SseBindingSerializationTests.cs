using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse;
using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse.Tests;

public class SseBindingSerializationTests
{
    private static JsonNode SerializeV3(AsyncApiBinding binding)
    {
        using var stringWriter = new StringWriter();
        var writer = new AsyncApiJsonWriter(stringWriter);
        binding.SerializeV3(writer);
        stringWriter.Flush();
        return JsonNode.Parse(stringWriter.ToString())!;
    }

    private static JsonNode SerializeV2(AsyncApiBinding binding)
    {
        using var stringWriter = new StringWriter();
        var writer = new AsyncApiJsonWriter(stringWriter);
        binding.SerializeV2(writer);
        stringWriter.Flush();
        return JsonNode.Parse(stringWriter.ToString())!;
    }

    [Fact]
    public void AllBindings_UseSseBindingKey()
    {
        new SseChannelBinding().BindingKey.ShouldBe("sse");
        new SseOperationBinding().BindingKey.ShouldBe("sse");
        new SseMessageBinding().BindingKey.ShouldBe("sse");
        new SseServerBinding().BindingKey.ShouldBe("sse");
    }

    [Fact]
    public void ChannelBinding_SerializesAllFields()
    {
        var binding = new SseChannelBinding
        {
            Path = "/events",
            Method = SseProtocol.Methods.Get,
            Query = new AsyncApiJsonSchema { Type = SchemaType.Object },
        };

        var json = SerializeV3(binding);

        json["path"]!.GetValue<string>().ShouldBe("/events");
        json["method"]!.GetValue<string>().ShouldBe("GET");
        json["contentType"]!.GetValue<string>().ShouldBe(SseProtocol.EventStreamContentType);
        json["query"].ShouldNotBeNull();
        json["bindingVersion"]!.GetValue<string>().ShouldBe(SseProtocol.DefaultBindingVersion);
    }

    [Fact]
    public void OperationBinding_DefaultsToServerToClient()
    {
        var binding = new SseOperationBinding { Method = SseProtocol.Methods.Get };

        var json = SerializeV3(binding);

        json["method"]!.GetValue<string>().ShouldBe("GET");
        json["direction"]!.GetValue<string>().ShouldBe("serverToClient");
    }

    [Fact]
    public void MessageBinding_SerializesEventFields()
    {
        var binding = new SseMessageBinding
        {
            Event = "price-update",
            Id = "42",
            Retry = 3000,
        };

        var json = SerializeV3(binding);

        json["event"]!.GetValue<string>().ShouldBe("price-update");
        json["id"]!.GetValue<string>().ShouldBe("42");
        json["retry"]!.GetValue<int>().ShouldBe(3000);
    }

    [Fact]
    public void ServerBinding_SerializesRetryAndHeartbeat()
    {
        var binding = new SseServerBinding { Retry = 5000, Heartbeat = true };

        var json = SerializeV3(binding);

        json["retry"]!.GetValue<int>().ShouldBe(5000);
        json["heartbeat"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void ExplicitBindingVersion_IsHonored()
    {
        var binding = new SseChannelBinding { Path = "/events", BindingVersion = "1.2.3" };

        SerializeV3(binding)["bindingVersion"]!.GetValue<string>().ShouldBe("1.2.3");
    }

    [Fact]
    public void V2AndV3_ProduceEquivalentMessageOutput()
    {
        var binding = new SseMessageBinding { Event = "ping", Retry = 1000 };

        SerializeV2(binding).ToJsonString().ShouldBe(SerializeV3(binding).ToJsonString());
    }
}
