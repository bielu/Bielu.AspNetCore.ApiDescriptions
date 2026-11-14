using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// The servers named in <see cref="ChannelAttribute.Servers"/> must each become a reference on the channel.
/// </summary>
public class ChannelServerReferenceTests
{
    private const string DocumentName = "channel-server-references";

    public sealed record ServerScopedEvent(string Id);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        // "prod" is a suffix of "eu-prod": the duplicate check used to treat it as already present.
        [Channel("server-scoped/suffix", Servers = ["eu-prod", "prod"])]
        [Message(typeof(ServerScopedEvent), MessageId = "serverScopedSuffix")]
        [SendOperation(OperationId = "serverScopedSuffixOperation")]
        public void SuffixNamedServers()
        {
        }

        [Channel("server-scoped/repeated", Servers = ["prod", "prod", "missing"])]
        [Message(typeof(ServerScopedEvent), MessageId = "serverScopedRepeated")]
        [SendOperation(OperationId = "serverScopedRepeatedOperation")]
        public void RepeatedAndUnknownServers()
        {
        }
    }

    [Fact]
    public async Task GetAsyncApiDocument_ServerNameIsSuffixOfAnother_ReferencesBothServers()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        ServerReferences(document, "server-scoped/suffix").ShouldBe(["#/servers/eu-prod", "#/servers/prod"]);
    }

    [Fact]
    public async Task GetAsyncApiDocument_RepeatedAndUnknownServers_ReferencesEachKnownServerOnce()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        ServerReferences(document, "server-scoped/repeated").ShouldBe(["#/servers/prod"]);
    }

    private static List<string?> ServerReferences(AsyncApiDocument document, string address) =>
        document.Channels.Values.Single(channel => channel.Address == address).Servers
            .Select(server => server.Reference?.Reference)
            .ToList();

    private static async Task<AsyncApiDocument> GetDocumentAsync()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName, options => options
            .AddServer("eu-prod", "eu.example.com:9092", "kafka")
            .AddServer("prod", "example.com:9092", "kafka"));

        await using var app = builder.Build();
        await app.StartAsync();

        var document = await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(DocumentName)
            .GetAsyncApiDocumentAsync();
        await app.StopAsync();

        return document;
    }
}
