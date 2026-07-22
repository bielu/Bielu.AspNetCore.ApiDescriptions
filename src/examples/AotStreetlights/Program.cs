using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Bielu.AspNetCore.AsyncApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register generated metadata to avoid reflection at runtime
builder.Services.AddAsyncApi("Streetlights");
builder.Services.AddAsyncApiGeneratedMetadata("Streetlights");

var app = builder.Build();

app.MapAsyncApi();
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
