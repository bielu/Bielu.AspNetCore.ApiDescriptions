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
}
