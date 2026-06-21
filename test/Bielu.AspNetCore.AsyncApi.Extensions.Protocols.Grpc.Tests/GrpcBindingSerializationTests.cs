using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc;
using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc.Tests;

public class GrpcBindingSerializationTests
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
    public void AllBindings_UseGrpcBindingKey()
    {
        new GrpcChannelBinding().BindingKey.ShouldBe("grpc");
        new GrpcOperationBinding().BindingKey.ShouldBe("grpc");
        new GrpcMessageBinding().BindingKey.ShouldBe("grpc");
        new GrpcServerBinding().BindingKey.ShouldBe("grpc");
    }

    [Fact]
    public void ChannelBinding_SerializesAllFields()
    {
        var binding = new GrpcChannelBinding
        {
            Service = "greet.Greeter",
            Package = "greet",
            ProtoFile = "Protos/greet.proto",
        };

        var json = SerializeV3(binding);

        json["service"]!.GetValue<string>().ShouldBe("greet.Greeter");
        json["package"]!.GetValue<string>().ShouldBe("greet");
        json["protoFile"]!.GetValue<string>().ShouldBe("Protos/greet.proto");
        json["bindingVersion"]!.GetValue<string>().ShouldBe(GrpcProtocol.DefaultBindingVersion);
    }

    [Fact]
    public void OperationBinding_SerializesAllFields()
    {
        var binding = new GrpcOperationBinding
        {
            Method = "SayHello",
            MethodType = GrpcMethodType.Unary,
            RequestType = "greet.HelloRequest",
            ResponseType = "greet.HelloReply",
            IdempotencyLevel = GrpcProtocol.IdempotencyLevels.NoSideEffects,
            DeadlineSeconds = 30,
        };

        var json = SerializeV3(binding);

        json["method"]!.GetValue<string>().ShouldBe("SayHello");
        json["methodType"]!.GetValue<string>().ShouldBe("unary");
        json["requestType"]!.GetValue<string>().ShouldBe("greet.HelloRequest");
        json["responseType"]!.GetValue<string>().ShouldBe("greet.HelloReply");
        json["idempotencyLevel"]!.GetValue<string>().ShouldBe("noSideEffects");
        json["deadlineSeconds"]!.GetValue<double>().ShouldBe(30);
        json["bindingVersion"]!.GetValue<string>().ShouldBe(GrpcProtocol.DefaultBindingVersion);
    }

    [Fact]
    public void MessageBinding_SerializesAllFields()
    {
        var binding = new GrpcMessageBinding
        {
            MessageType = "greet.HelloRequest",
            Encoding = GrpcProtocol.MessageEncodings.Protobuf,
            Headers = new AsyncApiJsonSchema { Type = SchemaType.Object },
        };

        var json = SerializeV3(binding);

        json["messageType"]!.GetValue<string>().ShouldBe("greet.HelloRequest");
        json["encoding"]!.GetValue<string>().ShouldBe("protobuf");
        json["headers"].ShouldNotBeNull();
    }

    [Theory]
    [InlineData(GrpcMethodType.Unary, "unary")]
    [InlineData(GrpcMethodType.ServerStreaming, "serverStreaming")]
    [InlineData(GrpcMethodType.ClientStreaming, "clientStreaming")]
    [InlineData(GrpcMethodType.BidirectionalStreaming, "bidirectionalStreaming")]
    public void MethodType_SerializesAsCamelCaseToken(GrpcMethodType type, string expected)
    {
        type.ToWireName().ShouldBe(expected);
        GrpcMethodTypeExtensions.Parse(expected).ShouldBe(type);
    }

    [Theory]
    [InlineData("UNARY", GrpcMethodType.Unary)]           // case-insensitive
    [InlineData("serverStreaming", GrpcMethodType.ServerStreaming)]
    [InlineData("nonsense", null)]
    [InlineData("", null)]
    public void MethodType_ParsesTolerantly(string value, GrpcMethodType? expected)
    {
        GrpcMethodTypeExtensions.Parse(value).ShouldBe(expected);
    }

    [Fact]
    public void ServerBinding_SerializesAllFields()
    {
        var binding = new GrpcServerBinding
        {
            Services = { "greet.Greeter" },
            Reflection = true,
            Tls = true,
            Compressions = { GrpcProtocol.Compressions.Gzip, GrpcProtocol.Compressions.Identity },
        };

        var json = SerializeV3(binding);

        json["services"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(new[] { "greet.Greeter" });
        json["reflection"]!.GetValue<bool>().ShouldBeTrue();
        json["tls"]!.GetValue<bool>().ShouldBeTrue();
        json["compressions"]!.AsArray().Select(n => n!.GetValue<string>())
            .ShouldBe(new[] { "gzip", "identity" });
    }

    [Fact]
    public void ExplicitBindingVersion_IsHonored()
    {
        var binding = new GrpcChannelBinding { Service = "greet.Greeter", BindingVersion = "1.2.3" };

        SerializeV3(binding)["bindingVersion"]!.GetValue<string>().ShouldBe("1.2.3");
    }

    [Fact]
    public void V2AndV3_ProduceEquivalentOperationOutput()
    {
        var binding = new GrpcOperationBinding { Method = "SayHello", MethodType = GrpcMethodType.Unary };

        SerializeV2(binding).ToJsonString().ShouldBe(SerializeV3(binding).ToJsonString());
    }
}
