using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Legacy;
using Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Modern;
using Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Orders;
using Bielu.AspNetCore.AsyncApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Fixtures;

public sealed record PlainEvent(string Id);

[AsyncApi(ProtobufTestApp.DocumentName)]
public sealed class ProtobufFixtureChannels
{
    [Channel("orders/placed")]
    [Message(typeof(OrderPlaced), MessageId = "orderPlaced")]
    [SendOperation(typeof(OrderPlaced), OperationId = "orderPlacedOperation")]
    public void OrderPlacedEvent()
    {
    }

    [Channel("legacy")]
    [Message(typeof(LegacyEvent), MessageId = "legacyEvent")]
    [SendOperation(typeof(LegacyEvent), OperationId = "legacyEventOperation")]
    public void LegacyEventEvent()
    {
    }

    [Channel("modern")]
    [Message(typeof(ModernEvent), MessageId = "modernEvent")]
    [SendOperation(typeof(ModernEvent), OperationId = "modernEventOperation")]
    public void ModernEventEvent()
    {
    }

    [Channel("plain")]
    [Message(typeof(PlainEvent), MessageId = "plainEvent")]
    [SendOperation(typeof(PlainEvent), OperationId = "plainEventOperation")]
    public void PlainEventEvent()
    {
    }
}

internal static class ProtobufTestApp
{
    public const string DocumentName = "protobuf";

    public static async Task<JsonDocument> GetDocumentAsync(Action<AsyncApiOptions> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName, options =>
        {
            options.WithInfo("Protobuf", "1.0.0");
            options.AddServer("grpc", "localhost:5001", "grpc");
            configure(options);
        });

        await using var app = builder.Build();
        app.MapAsyncApi();
        await app.StartAsync();

        using var response = await app.GetTestClient().GetAsync(
            AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", DocumentName));
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            // Surface the generator's own exception rather than the endpoint's problem details.
            await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(DocumentName).GetAsyncApiDocumentAsync();
            throw new InvalidOperationException($"The document endpoint returned {(int)response.StatusCode}: {json}");
        }

        await app.StopAsync();
        return JsonDocument.Parse(json);
    }
}
