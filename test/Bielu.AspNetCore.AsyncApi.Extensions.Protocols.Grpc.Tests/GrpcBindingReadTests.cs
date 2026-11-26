using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc;
using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Readers.Interface;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc.Tests;

/// <summary>
/// Tests the bindings' parsers (their fixed-field maps) by loading what the writers produce.
/// They call <c>LoadBinding</c> directly: ByteBard's document reader treats every <c>x-</c> key under
/// <c>bindings</c> as an extension, so it never hands an <c>x-grpc</c> binding to these parsers.
/// </summary>
public class GrpcBindingReadTests
{
    private static string Write(AsyncApiBinding binding, bool v2)
    {
        using var stringWriter = new StringWriter();
        var writer = new AsyncApiJsonWriter(stringWriter);
        if (v2)
        {
            binding.SerializeV2(writer);
        }
        else
        {
            binding.SerializeV3(writer);
        }

        stringWriter.Flush();
        return stringWriter.ToString();
    }

    private static T Load<T>(string json)
        where T : IBindingParser<T>, new()
    {
        var diagnostic = new AsyncApiDiagnostic();
        var node = new PropertyNode(new ParsingContext(diagnostic), GrpcProtocol.BindingKey, JsonNode.Parse(json)!);

        var binding = new T().LoadBinding(node);

        diagnostic.Errors.ShouldBeEmpty();
        return binding;
    }

    [Fact]
    public void ChannelBinding_RoundTrip_RestoresEveryField()
    {
        // Arrange
        var original = new GrpcChannelBinding
        {
            Service = "Greeter",
            Package = "greet",
            ProtoFile = "Protos/greet.proto",
            BindingVersion = "0.2.0",
        };

        // Act
        var binding = Load<GrpcChannelBinding>(Write(original, v2: false));

        // Assert
        binding.Service.ShouldBe("Greeter");
        binding.Package.ShouldBe("greet");
        binding.ProtoFile.ShouldBe("Protos/greet.proto");
        binding.BindingVersion.ShouldBe("0.2.0");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MessageBinding_RoundTrip_RestoresEveryField(bool v2)
    {
        // Arrange
        var original = new GrpcMessageBinding
        {
            MessageType = "greet.HelloRequest",
            Encoding = GrpcProtocol.MessageEncodings.Json,
            Headers = new AsyncApiJsonSchema { Type = SchemaType.Object },
        };

        // Act
        var binding = Load<GrpcMessageBinding>(Write(original, v2));

        // Assert
        binding.MessageType.ShouldBe("greet.HelloRequest");
        binding.Encoding.ShouldBe(GrpcProtocol.MessageEncodings.Json);
        binding.Headers.ShouldNotBeNull().Type.ShouldBe(SchemaType.Object);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OperationBinding_RoundTrip_RestoresEveryField(bool v2)
    {
        // Arrange
        var original = new GrpcOperationBinding
        {
            Method = "SayHello",
            MethodType = GrpcMethodType.BidirectionalStreaming,
            RequestType = "greet.HelloRequest",
            ResponseType = "greet.HelloReply",
            IdempotencyLevel = GrpcProtocol.IdempotencyLevels.NoSideEffects,
            DeadlineSeconds = 2.5,
        };

        // Act
        var binding = Load<GrpcOperationBinding>(Write(original, v2));

        // Assert
        binding.Method.ShouldBe("SayHello");
        binding.MethodType.ShouldBe(GrpcMethodType.BidirectionalStreaming);
        binding.RequestType.ShouldBe("greet.HelloRequest");
        binding.ResponseType.ShouldBe("greet.HelloReply");
        binding.IdempotencyLevel.ShouldBe(GrpcProtocol.IdempotencyLevels.NoSideEffects);
        binding.DeadlineSeconds.ShouldBe(2.5);
    }

    [Fact]
    public void ServerBinding_RoundTrip_RestoresEveryField()
    {
        // Arrange
        var original = new GrpcServerBinding
        {
            Services = ["greet.Greeter", "health.Health"],
            Reflection = true,
            Tls = false,
            Compressions = [GrpcProtocol.Compressions.Gzip, GrpcProtocol.Compressions.Identity],
        };

        // Act
        var binding = Load<GrpcServerBinding>(Write(original, v2: false));

        // Assert
        binding.Services.ShouldBe(["greet.Greeter", "health.Health"]);
        binding.Reflection.ShouldBe(true);
        binding.Tls.ShouldBe(false);
        binding.Compressions.ShouldBe([GrpcProtocol.Compressions.Gzip, GrpcProtocol.Compressions.Identity]);
        binding.BindingVersion.ShouldBe(GrpcProtocol.DefaultBindingVersion);
    }

    [Fact]
    public void LoadBinding_InvalidDeadlineAndMethodType_LeavesThemUnset()
    {
        // Arrange
        const string json = """{ "methodType": "fireAndForget", "deadlineSeconds": "soon" }""";

        // Act
        var binding = Load<GrpcOperationBinding>(json);

        // Assert
        binding.MethodType.ShouldBeNull();
        binding.DeadlineSeconds.ShouldBeNull();
    }

    [Theory]
    [InlineData(GrpcProtocol.MethodTypes.Unary, GrpcMethodType.Unary)]
    [InlineData(GrpcProtocol.MethodTypes.ServerStreaming, GrpcMethodType.ServerStreaming)]
    [InlineData(GrpcProtocol.MethodTypes.ClientStreaming, GrpcMethodType.ClientStreaming)]
    [InlineData(GrpcProtocol.MethodTypes.BidirectionalStreaming, GrpcMethodType.BidirectionalStreaming)]
    public void MethodTypeParse_WireName_RoundTrips(string wireName, GrpcMethodType expected)
    {
        GrpcMethodTypeExtensions.Parse(wireName).ShouldBe(expected);
        expected.ToWireName().ShouldBe(wireName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("42")]
    [InlineData("streaming")]
    public void MethodTypeParse_UnknownToken_ReturnsNull(string? token)
    {
        GrpcMethodTypeExtensions.Parse(token).ShouldBeNull();
    }

    [Fact]
    public void Serialize_NullWriter_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new GrpcChannelBinding().SerializeV3(null!));
        Should.Throw<ArgumentNullException>(() => new GrpcOperationBinding().SerializeV3(null!));
        Should.Throw<ArgumentNullException>(() => new GrpcMessageBinding().SerializeV3(null!));
        Should.Throw<ArgumentNullException>(() => new GrpcServerBinding().SerializeV3(null!));
    }

    [Fact]
    public void SerializeProperties_WritesTheSameAsSerializeV3()
    {
        // Arrange
        var bindings = new AsyncApiBinding[]
        {
            new GrpcChannelBinding { Service = "Greeter" },
            new GrpcOperationBinding { Method = "SayHello" },
            new GrpcMessageBinding { MessageType = "greet.HelloRequest" },
            new GrpcServerBinding { Reflection = true },
        };

        foreach (var binding in bindings)
        {
            using var stringWriter = new StringWriter();

            // Act
            binding.SerializeProperties(new AsyncApiJsonWriter(stringWriter));

            // Assert
            stringWriter.ToString().ShouldBe(Write(binding, v2: false));
        }
    }
}
