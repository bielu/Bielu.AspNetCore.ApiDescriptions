using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Kafka;
using Wolverine.SignalR;

namespace Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures;

/// <summary>
/// Boots a test-server app with Wolverine routing to SignalR and (stubbed) Kafka, and fetches its
/// AsyncAPI documents over HTTP.
/// </summary>
internal static class WolverineTestApp
{
    public const string HubPath = "/hubs/test";

    public static async Task<JsonNode> GetDocumentAsync(
        string documentName,
        Action<AsyncApiOptions> configureDocument,
        bool mapHub = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Host.UseWolverine(opts =>
        {
            opts.ApplicationAssembly = typeof(WolverineTestApp).Assembly;
            opts.UseSignalR<TestHub>();
            opts.Publish(rule => rule.MessagesImplementing<WebSocketMessage>().ToSignalR());

            opts.UseKafka("localhost:9092");
            opts.PublishMessage<OrderPlaced>().ToKafkaTopic("orders");
            opts.ListenToKafkaTopic("stock").DefaultIncomingMessage<StockChanged>();

            opts.StubAllExternalTransports();
        });

        builder.Services.AddAsyncApi(documentName, configureDocument);

        await using var app = builder.Build();
        if (mapHub)
        {
            app.MapWolverineSignalRHub<TestHub>(HubPath);
        }

        app.MapAsyncApi();

        await app.StartAsync();
        try
        {
            using var client = app.GetTestClient();
            using var response = await client.GetAsync($"/asyncapi/{documentName}.json");
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                // Surface the generator's own exception rather than the endpoint's problem details.
                await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(documentName).GetAsyncApiDocumentAsync();
                throw new InvalidOperationException($"GET /asyncapi/{documentName}.json returned {(int)response.StatusCode}: {json}");
            }

            return JsonNode.Parse(json)!;
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
