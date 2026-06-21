using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;
using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR.Tests;

public class SignalRBindingSerializationTests
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
    public void AllBindings_UseSignalRBindingKey()
    {
        new SignalRChannelBinding().BindingKey.ShouldBe("signalr");
        new SignalROperationBinding().BindingKey.ShouldBe("signalr");
        new SignalRMessageBinding().BindingKey.ShouldBe("signalr");
        new SignalRServerBinding().BindingKey.ShouldBe("signalr");
    }

    [Fact]
    public void ChannelBinding_SerializesAllFields()
    {
        var binding = new SignalRChannelBinding
        {
            Hub = "/chatHub",
            Transports = { SignalRProtocol.Transports.WebSockets, SignalRProtocol.Transports.LongPolling },
            Protocols = { SignalRProtocol.HubProtocols.Json },
            Query = new AsyncApiJsonSchema { Type = SchemaType.Object },
        };

        var json = SerializeV3(binding);

        json["hub"]!.GetValue<string>().ShouldBe("/chatHub");
        json["transports"]!.AsArray().Select(n => n!.GetValue<string>())
            .ShouldBe(new[] { "webSockets", "longPolling" });
        json["protocols"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(new[] { "json" });
        json["query"].ShouldNotBeNull();
        json["bindingVersion"]!.GetValue<string>().ShouldBe(SignalRProtocol.DefaultBindingVersion);
    }

    [Fact]
    public void OperationBinding_SerializesAllFields()
    {
        var binding = new SignalROperationBinding
        {
            Target = "SendMessage",
            Direction = SignalRProtocol.Directions.ClientToServer,
            CallType = SignalRProtocol.CallTypes.Invocation,
            Streaming = true,
        };

        var json = SerializeV3(binding);

        json["target"]!.GetValue<string>().ShouldBe("SendMessage");
        json["direction"]!.GetValue<string>().ShouldBe("clientToServer");
        json["callType"]!.GetValue<string>().ShouldBe("invocation");
        json["streaming"]!.GetValue<bool>().ShouldBeTrue();
        json["bindingVersion"]!.GetValue<string>().ShouldBe(SignalRProtocol.DefaultBindingVersion);
    }

    [Fact]
    public void MessageBinding_SerializesAllFields()
    {
        var binding = new SignalRMessageBinding
        {
            HubProtocol = SignalRProtocol.HubProtocols.MessagePack,
            MessageType = SignalRMessageType.Invocation,
        };

        var json = SerializeV3(binding);

        json["hubProtocol"]!.GetValue<string>().ShouldBe("messagepack");
        json["messageType"]!.GetValue<string>().ShouldBe("invocation");
    }

    [Theory]
    [InlineData(SignalRMessageType.Invocation, "invocation")]
    [InlineData(SignalRMessageType.StreamInvocation, "streamInvocation")]
    [InlineData(SignalRMessageType.CancelInvocation, "cancelInvocation")]
    public void MessageType_SerializesAsCamelCaseToken(SignalRMessageType type, string expected)
    {
        type.ToWireName().ShouldBe(expected);
        SignalRMessageTypeExtensions.Parse(expected).ShouldBe(type);
    }

    [Theory]
    [InlineData("1", SignalRMessageType.Invocation)]   // legacy numeric wire id
    [InlineData("Completion", SignalRMessageType.Completion)] // case-insensitive
    [InlineData("99", null)]                            // out of range
    [InlineData("nonsense", null)]
    public void MessageType_ParsesTolerantly(string value, SignalRMessageType? expected)
    {
        SignalRMessageTypeExtensions.Parse(value).ShouldBe(expected);
    }

    [Fact]
    public void ServerBinding_SerializesTransportsAndProtocols()
    {
        var binding = new SignalRServerBinding
        {
            Transports = { SignalRProtocol.Transports.WebSockets },
            Protocols = { SignalRProtocol.HubProtocols.Json, SignalRProtocol.HubProtocols.MessagePack },
        };

        var json = SerializeV3(binding);

        json["transports"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(new[] { "webSockets" });
        json["protocols"]!.AsArray().Select(n => n!.GetValue<string>())
            .ShouldBe(new[] { "json", "messagepack" });
    }

    [Fact]
    public void ExplicitBindingVersion_IsHonored()
    {
        var binding = new SignalRChannelBinding { Hub = "/hub", BindingVersion = "1.2.3" };

        SerializeV3(binding)["bindingVersion"]!.GetValue<string>().ShouldBe("1.2.3");
    }

    [Fact]
    public void V2AndV3_ProduceEquivalentOperationOutput()
    {
        var binding = new SignalROperationBinding { Target = "Ping", CallType = SignalRProtocol.CallTypes.Send };

        SerializeV2(binding).ToJsonString().ShouldBe(SerializeV3(binding).ToJsonString());
    }
}
