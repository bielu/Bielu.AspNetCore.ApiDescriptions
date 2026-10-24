using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// The served 2.x document must list a channel's servers by name and write a message trait that has no headers.
/// Both used to need workarounds here, until ByteBard fixed its 2.x writer
/// (ByteBardOrg/AsyncAPI.NET#57 and #58).
/// </summary>
public class AsyncApiV2WriterTests
{
    private const string DocumentName = "v2-writer";

    public sealed record OrderPlaced(string OrderId);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("orders/placed", Servers = ["broker"])]
        [Message(typeof(OrderPlaced), MessageId = "orderPlaced", Traits = ["traced"])]
        [SubscribeOperation(OperationId = "orderPlacedOperation")]
        public void OrderPlacedEvent()
        {
        }
    }

    private static void Configure(AsyncApiOptions options)
    {
        options.AsyncApiVersion = AsyncApiVersion.AsyncApi2_0;
        options
            .AddServer("broker", "localhost:9092", "kafka")
            .AddMessageTrait("traced", new AsyncApiMessageTrait { ContentType = "application/json" });
    }

    [Fact(Skip = "ByteBard 4.0.0-beta.20 throws writing 2.x message traits; re-enable once ByteBardOrg/AsyncAPI.NET#57 and #58 ship.")]
    public async Task GetAsyncApiDocument_V2ChannelServers_AreServedAsServerNames()
    {
        // Act
        using var document = await GetServedDocumentAsync();

        // Assert
        var servers = document.RootElement.GetProperty("channels").GetProperty("orders/placed")
            .GetProperty("servers").EnumerateArray().Select(s => s.GetString()).ToList();
        servers.ShouldBe(["broker"]);
    }

    [Fact(Skip = "ByteBard 4.0.0-beta.20 throws writing 2.x message traits; re-enable once ByteBardOrg/AsyncAPI.NET#57 and #58 ship.")]
    public async Task GetAsyncApiDocument_V2MessageTraitWithoutHeaders_IsServedWithoutHeaders()
    {
        // Act
        using var document = await GetServedDocumentAsync();

        // Assert
        var trait = document.RootElement.GetProperty("components").GetProperty("messageTraits").GetProperty("traced");
        trait.GetProperty("contentType").GetString().ShouldBe("application/json");
        trait.TryGetProperty("headers", out _).ShouldBeFalse();
    }

    private static async Task<JsonDocument> GetServedDocumentAsync()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName, Configure);

        await using var app = builder.Build();
        app.MapAsyncApi();
        await app.StartAsync();

        var json = await app.GetTestClient().GetStringAsync(
            AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", DocumentName));
        await app.StopAsync();

        return JsonDocument.Parse(json);
    }
}
