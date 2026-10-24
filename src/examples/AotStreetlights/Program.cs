using System.Text.Json.Serialization;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;

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
    [Message(typeof(LightMeasurement), CorrelationIdLocation = "$message.header#/correlationId", CorrelationIdDescription = "Correlates a measurement with the \"measure\" request that triggered it.")]
    public void MeasureLight(object message) { }
}

// Class-level [Message]: picked up by the source generator, so it applies under Native AOT without reflection.
[Message(Title = "Light measurement", Summary = "A lumens reading taken by a streetlight.")]
public record LightMeasurement(int Lumens, DateTime MeasuredAt);

[JsonSerializable(typeof(LightMeasurement))]
internal partial class StreetlightsContext : JsonSerializerContext { }
