using Bielu.AspNetCore.Arazzo.SourceResolvers;
using ByteBard.AsyncAPI.Models;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Unit;

public class AsyncApiSourceResolverTests
{
    private static AsyncApiDocument CreateDocument(Dictionary<string, AsyncApiChannel> channels) => new()
    {
        Asyncapi = "3.0.0",
        Info = new AsyncApiInfo { Title = "Test", Version = "1.0.0" },
        Channels = channels,
        Operations = new Dictionary<string, AsyncApiOperation>(),
    };

    [Fact]
    public void TryResolveChannelPath_EscapedSlashInChannelName_Resolves()
    {
        var document = CreateDocument(new Dictionary<string, AsyncApiChannel> { ["user/signedup"] = new() });
        var resolver = new AsyncApiSourceResolver();

        var resolved = resolver.TryResolveChannelPath(document, "/channels/user~1signedup", out var channel);

        resolved.ShouldBeTrue();
        channel.ShouldNotBeNull();
    }

    [Fact]
    public void TryResolveChannelPath_RawSlashInPointer_DoesNotResolve()
    {
        // A raw (unescaped) '/' in a JSON Pointer segment always means "one level deeper" (RFC 6901); it
        // must not be silently treated as part of a flat channel name, even if a channel with that literal
        // slash-containing name exists.
        var document = CreateDocument(new Dictionary<string, AsyncApiChannel> { ["user/signedup"] = new() });
        var resolver = new AsyncApiSourceResolver();

        var resolved = resolver.TryResolveChannelPath(document, "/channels/user/signedup", out var channel);

        resolved.ShouldBeFalse();
        channel.ShouldBeNull();
    }

    [Fact]
    public void TryResolveChannelPath_UnknownChannel_DoesNotResolve()
    {
        var document = CreateDocument(new Dictionary<string, AsyncApiChannel>());
        var resolver = new AsyncApiSourceResolver();

        var resolved = resolver.TryResolveChannelPath(document, "/channels/doesNotExist", out var channel);

        resolved.ShouldBeFalse();
        channel.ShouldBeNull();
    }

    private static AsyncApiDocument CreateDocumentWithOperation()
    {
        var document = CreateDocument(new Dictionary<string, AsyncApiChannel> { ["orders"] = new() { Address = "orders" } });
        document.Operations["placeOrder"] = new AsyncApiOperation
        {
            Action = AsyncApiAction.Send,
            Channel = new AsyncApiChannelReference("#/channels/orders"),
        };
        return document;
    }

    [Fact]
    public void SourceType_IsAsyncApi()
    {
        new AsyncApiSourceResolver().SourceType.ShouldBe("asyncapi");
    }

    [Fact]
    public void TryResolveOperation_KnownOperationId_ReturnsTheSerializedOperation()
    {
        // Act
        var resolved = new AsyncApiSourceResolver().TryResolveOperation(CreateDocumentWithOperation(), "placeOrder", out var operation);

        // Assert
        resolved.ShouldBeTrue();
        operation.ShouldNotBeNull()["action"]!.GetValue<string>().ShouldBe("send");
    }

    [Fact]
    public void TryResolveOperation_UnknownOperationId_DoesNotResolve()
    {
        new AsyncApiSourceResolver().TryResolveOperation(CreateDocumentWithOperation(), "cancelOrder", out var operation).ShouldBeFalse();
        operation.ShouldBeNull();
    }

    [Theory]
    [InlineData("/operations/placeOrder", true)]
    [InlineData("/operations/cancelOrder", false)]
    [InlineData("/channels/orders", false)]
    public void TryResolveOperationPath_ResolvesOnlyKnownOperationPointers(string pointer, bool expected)
    {
        // Act
        var resolved = new AsyncApiSourceResolver().TryResolveOperationPath(CreateDocumentWithOperation(), pointer, out var operation);

        // Assert
        resolved.ShouldBe(expected);
        (operation is not null).ShouldBe(expected);
    }

    [Fact]
    public void TryResolveChannelPath_KnownChannel_ReturnsTheSerializedChannel()
    {
        // Act
        var resolved = new AsyncApiSourceResolver().TryResolveChannelPath(CreateDocumentWithOperation(), "/channels/orders", out var channel);

        // Assert
        resolved.ShouldBeTrue();
        channel.ShouldNotBeNull()["address"]!.GetValue<string>().ShouldBe("orders");
    }

    [Fact]
    public void TryResolveChannelPath_PointerOutsideChannels_DoesNotResolve()
    {
        new AsyncApiSourceResolver().TryResolveChannelPath(CreateDocumentWithOperation(), "/operations/placeOrder", out _).ShouldBeFalse();
    }

    [Fact]
    public void TryResolve_NullArguments_Throw()
    {
        var resolver = new AsyncApiSourceResolver();
        var document = CreateDocumentWithOperation();

        Should.Throw<ArgumentNullException>(() => resolver.TryResolveOperation(null!, "x", out _));
        Should.Throw<ArgumentNullException>(() => resolver.TryResolveOperation(document, null!, out _));
        Should.Throw<ArgumentNullException>(() => resolver.TryResolveOperationPath(null!, "/operations/x", out _));
        Should.Throw<ArgumentNullException>(() => resolver.TryResolveOperationPath(document, null!, out _));
        Should.Throw<ArgumentNullException>(() => resolver.TryResolveChannelPath(null!, "/channels/x", out _));
        Should.Throw<ArgumentNullException>(() => resolver.TryResolveChannelPath(document, null!, out _));
    }
}
