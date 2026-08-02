using Bielu.AspNetCore.AsyncApi.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add AsyncAPI services
builder.Services.AddAsyncApi(options =>
{
    options.WithInfo("AsyncApiWebApi", "1.0.0")
           .WithDescription("A sample Web API with AsyncAPI documentation")
           .IncludeXmlComments(typeof(Program).Assembly);
    
    options.AddServer("mosquitto", "test.mosquitto.org", "mqtt", pathName: null, server =>
    {
        server.Description = "Test Mosquitto MQTT Broker";
    });
});

builder.Services.AddControllers();

var app = builder.Build();

app.MapAsyncApi();
app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("v1", "AsyncApiWebApi", "/asyncapi/v1.json");
});

app.MapControllers();

app.Run();
