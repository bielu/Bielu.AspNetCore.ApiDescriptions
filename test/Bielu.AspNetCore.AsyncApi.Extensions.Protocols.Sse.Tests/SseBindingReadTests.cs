using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse;
using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Readers.Interface;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse.Tests;

/// <summary>
/// Tests the bindings' parsers (their fixed-field maps) by loading what the writers produce.
/// They call <c>LoadBinding</c> directly: ByteBard's document reader treats every <c>x-</c> key under
/// <c>bindings</c> as an extension, so it never hands an <c>x-sse</c> binding to these parsers.
/// </summary>
public class SseBindingReadTests
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
        var node = new PropertyNode(new ParsingContext(diagnostic), SseProtocol.BindingKey, JsonNode.Parse(json)!);

        var binding = new T().LoadBinding(node);

        diagnostic.Errors.ShouldBeEmpty();
        return binding;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChannelBinding_RoundTrip_RestoresEveryField(bool v2)
    {
        // Arrange
        var original = new SseChannelBinding
        {
            Path = "/events",
            Method = SseProtocol.Methods.Get,
            ContentType = "text/event-stream; charset=utf-8",
            Query = new AsyncApiJsonSchema { Type = SchemaType.Object },
            Headers = new AsyncApiJsonSchema { Type = SchemaType.Object },
            BindingVersion = "0.2.0",
        };

        // Act
        var binding = Load<SseChannelBinding>(Write(original, v2));

        // Assert
        binding.Path.ShouldBe("/events");
        binding.Method.ShouldBe("GET");
        binding.ContentType.ShouldBe("text/event-stream; charset=utf-8");
        binding.Query.ShouldNotBeNull().Type.ShouldBe(SchemaType.Object);
        binding.Headers.ShouldNotBeNull().Type.ShouldBe(SchemaType.Object);
        binding.BindingVersion.ShouldBe("0.2.0");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OperationBinding_RoundTrip_RestoresEveryField(bool v2)
    {
        // Arrange
        var original = new SseOperationBinding { Method = SseProtocol.Methods.Get, Direction = "custom" };

        // Act
        var binding = Load<SseOperationBinding>(Write(original, v2));

        // Assert
        binding.Method.ShouldBe("GET");
        binding.Direction.ShouldBe("custom");
        binding.BindingVersion.ShouldBe(SseProtocol.DefaultBindingVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MessageBinding_RoundTrip_RestoresEveryField(bool v2)
    {
        // Arrange
        var original = new SseMessageBinding
        {
            Event = "price-update",
            Id = "42",
            Retry = 3000,
            Headers = new AsyncApiJsonSchema { Type = SchemaType.Object },
        };

        // Act
        var binding = Load<SseMessageBinding>(Write(original, v2));

        // Assert
        binding.Event.ShouldBe("price-update");
        binding.Id.ShouldBe("42");
        binding.Retry.ShouldBe(3000);
        binding.Headers.ShouldNotBeNull().Type.ShouldBe(SchemaType.Object);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ServerBinding_RoundTrip_RestoresEveryField(bool v2)
    {
        // Arrange
        var original = new SseServerBinding { Retry = 5000, Heartbeat = true };

        // Act
        var binding = Load<SseServerBinding>(Write(original, v2));

        // Assert
        binding.Retry.ShouldBe(5000);
        binding.Heartbeat.ShouldBe(true);
    }

    [Fact]
    public void LoadBinding_NonNumericRetry_LeavesRetryUnset()
    {
        // Arrange
        const string json = """{ "retry": "soon" }""";

        // Act
        var server = Load<SseServerBinding>(json);
        var message = Load<SseMessageBinding>(json);

        // Assert
        server.Retry.ShouldBeNull();
        message.Retry.ShouldBeNull();
    }

    [Fact]
    public void LoadBinding_ExtensionField_IsKeptAsExtension()
    {
        // Arrange
        const string json = """{ "path": "/events", "x-owner": "pricing" }""";

        // Act
        var binding = Load<SseChannelBinding>(json);

        // Assert
        binding.Extensions.ShouldContainKey("x-owner");
        Write(binding, v2: false).ShouldContain("\"x-owner\"");
    }

    [Fact]
    public void SerializeProperties_WritesTheSameAsSerializeV3()
    {
        // Arrange
        var bindings = new AsyncApiBinding[]
        {
            new SseChannelBinding { Path = "/events" },
            new SseOperationBinding { Method = SseProtocol.Methods.Get },
            new SseMessageBinding { Event = "ping" },
            new SseServerBinding { Heartbeat = false },
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

    [Fact]
    public void Serialize_NullWriter_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new SseChannelBinding().SerializeV3(null!));
        Should.Throw<ArgumentNullException>(() => new SseOperationBinding().SerializeV3(null!));
        Should.Throw<ArgumentNullException>(() => new SseMessageBinding().SerializeV3(null!));
        Should.Throw<ArgumentNullException>(() => new SseServerBinding().SerializeV3(null!));
    }
}
