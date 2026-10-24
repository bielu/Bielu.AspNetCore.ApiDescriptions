using System.Text.Json.Nodes;
using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc.Tests;

/// <summary>
/// Reads every gRPC binding (server, channel, operation, message) from a document with all fields set, then writes
/// each back out: the bindings must parse into their typed form and serialize to exactly what was read.
/// </summary>
public class GrpcBindingReadRoundTripTests
{
    private const string Document = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "gRPC", "version": "1.0.0" },
          "servers": {
            "api": {
              "host": "grpc.example.com",
              "protocol": "grpc",
              "bindings": {
                "grpc": { "services": ["greet.Greeter"], "reflection": true, "tls": true, "compressions": ["gzip"], "bindingVersion": "0.1.0" }
              }
            }
          },
          "channels": {
            "greeter": {
              "address": "greet.Greeter",
              "bindings": { "grpc": { "service": "Greeter", "package": "greet", "protoFile": "Protos/greet.proto", "bindingVersion": "0.1.0" } },
              "messages": {
                "helloReply": {
                  "payload": { "type": "object" },
                  "bindings": {
                    "grpc": { "messageType": "greet.HelloReply", "encoding": "protobuf", "headers": { "type": "object" }, "bindingVersion": "0.1.0" }
                  }
                }
              }
            }
          },
          "operations": {
            "sayHello": {
              "action": "receive",
              "channel": { "$ref": "#/channels/greeter" },
              "bindings": {
                "grpc": {
                  "method": "SayHello",
                  "methodType": "serverStreaming",
                  "requestType": "greet.HelloRequest",
                  "responseType": "greet.HelloReply",
                  "idempotencyLevel": "NO_SIDE_EFFECTS",
                  "deadlineSeconds": 2.5,
                  "bindingVersion": "0.1.0"
                }
              }
            }
          }
        }
        """;

    private static AsyncApiDocument Read()
    {
        var settings = new AsyncApiReaderSettings();
        settings.Bindings.Add(new GrpcServerBinding());
        settings.Bindings.Add(new GrpcChannelBinding());
        settings.Bindings.Add(new GrpcOperationBinding());
        settings.Bindings.Add(new GrpcMessageBinding());

        var document = new AsyncApiStringReader(settings).Read(Document, out var diagnostic);
        diagnostic.Errors.ShouldBeEmpty();
        return document;
    }

    private static JsonNode Write(AsyncApiBinding binding, bool v2)
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

        return JsonNode.Parse(stringWriter.ToString())!;
    }

    private static void ShouldRoundTrip(AsyncApiBinding binding, string pointer)
    {
        var expected = JsonNode.Parse(Document)!;
        foreach (var segment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            expected = expected[segment]!;
        }

        JsonNode.DeepEquals(Write(binding, v2: false), expected).ShouldBeTrue($"V3 {pointer}: {Write(binding, v2: false).ToJsonString()}");
        JsonNode.DeepEquals(Write(binding, v2: true), expected).ShouldBeTrue($"V2 {pointer}: {Write(binding, v2: true).ToJsonString()}");
    }

    [Fact]
    public void Read_AllBindings_ParseEveryField()
    {
        // Act
        var document = Read();

        // Assert
        var server = document.Servers["api"].Bindings.Values.OfType<GrpcServerBinding>().ShouldHaveSingleItem();
        server.Services.ShouldBe(["greet.Greeter"]);
        server.Reflection.ShouldBe(true);
        server.Tls.ShouldBe(true);
        server.Compressions.ShouldBe(["gzip"]);

        var channel = document.Channels["greeter"].Bindings.Values.OfType<GrpcChannelBinding>().ShouldHaveSingleItem();
        channel.Service.ShouldBe("Greeter");
        channel.Package.ShouldBe("greet");
        channel.ProtoFile.ShouldBe("Protos/greet.proto");

        var operation = document.Operations["sayHello"].Bindings.Values.OfType<GrpcOperationBinding>().ShouldHaveSingleItem();
        operation.MethodType.ShouldBe(GrpcMethodType.ServerStreaming);
        operation.DeadlineSeconds.ShouldBe(2.5);

        var message = document.Channels["greeter"].Messages["helloReply"].Bindings.Values.OfType<GrpcMessageBinding>().ShouldHaveSingleItem();
        message.MessageType.ShouldBe("greet.HelloReply");
        message.Headers.ShouldNotBeNull();
    }

    [Fact]
    public void Write_ReadBindings_ReproduceTheSourceJson()
    {
        // Arrange
        var document = Read();

        // Act & Assert
        ShouldRoundTrip(document.Servers["api"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/servers/api/bindings/grpc");
        ShouldRoundTrip(document.Channels["greeter"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/channels/greeter/bindings/grpc");
        ShouldRoundTrip(document.Operations["sayHello"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/operations/sayHello/bindings/grpc");
        ShouldRoundTrip(document.Channels["greeter"].Messages["helloReply"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/channels/greeter/messages/helloReply/bindings/grpc");
    }

    [Fact]
    public void Read_UnknownMethodTypeOrDeadline_LeavesThemUnset()
    {
        // Arrange
        var settings = new AsyncApiReaderSettings();
        settings.Bindings.Add(new GrpcOperationBinding());
        var json = Document.Replace("\"serverStreaming\"", "\"sideways\"").Replace("2.5", "\"soon\"");

        // Act
        var document = new AsyncApiStringReader(settings).Read(json, out _);

        // Assert
        var operation = document.Operations["sayHello"].Bindings.Values.OfType<GrpcOperationBinding>().ShouldHaveSingleItem();
        operation.MethodType.ShouldBeNull();
        operation.DeadlineSeconds.ShouldBeNull();
    }
}
