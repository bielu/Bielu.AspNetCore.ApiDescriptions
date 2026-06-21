using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc.Tests;

/// <summary>
/// End-to-end example: builds a complete AsyncAPI document describing a gRPC greeter service using all
/// four gRPC bindings (server, channel, operation, message) and serializes it to AsyncAPI v3 JSON.
/// </summary>
public class GrpcDocumentExampleTests
{
    private static AsyncApiDocument BuildGreeterDocument()
    {
        // 1. Server: a gRPC endpoint advertising the services it hosts and its capabilities.
        var server = new AsyncApiServer
        {
            Host = "localhost:5001",
            Protocol = GrpcProtocol.ProtocolName,
            Bindings = new AsyncApiBindings<IServerBinding>
            {
                new GrpcServerBinding
                {
                    Services = { "greet.Greeter" },
                    Reflection = true,
                    Tls = true,
                    Compressions = { GrpcProtocol.Compressions.Gzip },
                },
            },
        };

        // 2. Message: the protobuf request frame.
        var helloRequest = new AsyncApiMessage
        {
            Name = "HelloRequest",
            Bindings = new AsyncApiBindings<IMessageBinding>
            {
                new GrpcMessageBinding
                {
                    MessageType = "greet.HelloRequest",
                    Encoding = GrpcProtocol.MessageEncodings.Protobuf,
                },
            },
        };

        // 3. Channel: the greeter service itself.
        var channel = new AsyncApiChannel
        {
            Address = "greet.Greeter",
            Messages = { ["helloRequest"] = helloRequest },
            Bindings = new AsyncApiBindings<IChannelBinding>
            {
                new GrpcChannelBinding
                {
                    Service = "greet.Greeter",
                    Package = "greet",
                    ProtoFile = "Protos/greet.proto",
                },
            },
        };

        // 4. Operation: the unary "SayHello" RPC.
        var operation = new AsyncApiOperation
        {
            Action = AsyncApiAction.Send,
            Channel = new AsyncApiChannelReference("#/channels/greeter"),
            Bindings = new AsyncApiBindings<IOperationBinding>
            {
                new GrpcOperationBinding
                {
                    Method = "SayHello",
                    MethodType = GrpcMethodType.Unary,
                    RequestType = "greet.HelloRequest",
                    ResponseType = "greet.HelloReply",
                },
            },
        };

        var document = new AsyncApiDocument
        {
            Info = new AsyncApiInfo { Title = "Greeter", Version = "1.0.0" },
        };
        document.Servers["grpc"] = server;
        document.Channels["greeter"] = channel;
        document.Operations["sayHello"] = operation;
        return document;
    }

    [Fact]
    public void GreeterDocument_SerializesWithGrpcBindings()
    {
        var document = BuildGreeterDocument();

        using var stringWriter = new StringWriter();
        document.SerializeV3(new AsyncApiJsonWriter(stringWriter));
        stringWriter.Flush();
        var json = JsonNode.Parse(stringWriter.ToString())!;

        json["servers"]!["grpc"]!["bindings"]!["grpc"]!["services"]!
            .AsArray().Select(n => n!.GetValue<string>()).ShouldContain("greet.Greeter");

        json["channels"]!["greeter"]!["bindings"]!["grpc"]!["service"]!
            .GetValue<string>().ShouldBe("greet.Greeter");

        json["operations"]!["sayHello"]!["bindings"]!["grpc"]!["method"]!
            .GetValue<string>().ShouldBe("SayHello");

        json["channels"]!["greeter"]!["messages"]!["helloRequest"]!["bindings"]!["grpc"]!["messageType"]!
            .GetValue<string>().ShouldBe("greet.HelloRequest");
    }
}
