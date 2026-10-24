using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.AsyncApi.Avro.Tests.Fixtures;

internal static class AvroTestApp
{
    public static async Task<JsonDocument> GetDocumentAsync(Action<AsyncApiOptions> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(AvroFixtureChannels.DocumentName, options =>
        {
            options.WithInfo("Avro", "1.0.0");
            options.AddServer("kafka", "localhost:9092", "kafka");
            configure(options);
        });

        await using var app = builder.Build();
        app.MapAsyncApi();
        await app.StartAsync();

        var json = await app.GetTestClient().GetStringAsync(
            AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", AvroFixtureChannels.DocumentName));
        await app.StopAsync();
        return JsonDocument.Parse(json);
    }
}
