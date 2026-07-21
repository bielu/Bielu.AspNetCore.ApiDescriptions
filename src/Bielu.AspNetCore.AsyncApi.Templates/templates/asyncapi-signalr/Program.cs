using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Scalar.SignalR;
using AsyncApiSignalR;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add SignalR
builder.Services.AddSignalR();

// Add AsyncAPI services with SignalR support
builder.Services.AddAsyncApi(options =>
{
    options.WithInfo("AsyncApiSignalR", "1.0.0")
           .WithDescription("A sample SignalR Hub with AsyncAPI documentation")
           .IncludeXmlComments(typeof(Program).Assembly);
});

var app = builder.Build();

app.MapAsyncApi();
app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("v1", "AsyncApiSignalR", "/asyncapi/v1.json");
    // Enable the interactive SignalR console
    options.WithSignalRClient();
});

app.MapHub<ChatHub>("/chat");

app.Run();
