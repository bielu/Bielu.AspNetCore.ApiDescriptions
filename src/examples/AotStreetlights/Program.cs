using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Register generated metadata to avoid reflection at runtime
builder.Services.AddAsyncApi("Streetlights");
builder.Services.AddAsyncApiGeneratedMetadata("Streetlights");

// Configure JSON options for AOT
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, StreetlightsContext.Default);
});

var app = builder.Build();

app.MapAsyncApi();
app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("Streetlights", "Streetlights API", "/asyncapi/Streetlights.json");
});
app.MapGet("/", () => "AOT Streetlights API");

app.Run();

[AsyncApi("Streetlights")]
public class LightService
{
    [Channel("light/measured")]
    [Message(typeof(LightMeasurement))]
    public void MeasureLight(object message) { }
}

public record LightMeasurement(int Lumens, DateTime MeasuredAt);

[JsonSerializable(typeof(LightMeasurement))]
internal partial class StreetlightsContext : JsonSerializerContext { }
