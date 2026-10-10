using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Helpers;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// The request/reply properties on the operation attributes must produce the operation's <c>reply</c>.
/// </summary>
public class OperationReplyTests
{
    private const string DocumentName = "operation-reply";

    public sealed record PingRequest(string Id);

    public sealed record PingReply(string Id);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("reply/ping")]
        [Message(typeof(PingRequest), MessageId = "pingRequest")]
        [SubscribeOperation(typeof(PingRequest), OperationId = "pingOperation",
            ReplyChannel = "reply/pong",
            ReplyAddressLocation = "$message.header#/replyTo",
            ReplyAddressDescription = "Where the pong goes.",
            ReplyMessageIds = ["pongReply"])]
        public void Ping()
        {
        }

        [Channel("reply/pong")]
        [Message(typeof(PingReply), MessageId = "pongReply")]
        [PublishOperation(typeof(PingReply), OperationId = "pongOperation")]
        public void Pong()
        {
        }
    }

    [Fact]
    public async Task GetAsyncApiDocument_OperationWithReply_WritesReply()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var reply = document.Operations["pingOperation"].Reply;
        reply.ShouldNotBeNull();
        reply.Address.Location.ShouldBe("$message.header#/replyTo");
        reply.Address.Description.ShouldBe("Where the pong goes.");
        var channelKey = AsyncApiNamingHelper.SanitizeKey("reply/pong");
        reply.Channel.Reference.Reference.ShouldBe($"#/channels/{channelKey}");
        reply.Messages.Select(m => m.Reference.Reference).ShouldBe([$"#/channels/{channelKey}/messages/pongReply"]);
    }

    [Fact]
    public async Task GetAsyncApiDocument_OperationWithoutReply_HasNoReply()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        document.Operations["pongOperation"].Reply.ShouldBeNull();
    }

    private static async Task<AsyncApiDocument> GetDocumentAsync()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName);

        await using var app = builder.Build();
        await app.StartAsync();

        var document = await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(DocumentName)
            .GetAsyncApiDocumentAsync();
        await app.StopAsync();

        return document;
    }
}
