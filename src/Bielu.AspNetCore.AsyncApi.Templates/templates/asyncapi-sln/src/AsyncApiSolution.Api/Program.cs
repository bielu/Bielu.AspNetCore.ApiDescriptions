using Bielu.AspNetCore.AsyncApi.Extensions;
using AsyncApiSolution.Contracts;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add AsyncAPI services
builder.Services.AddAsyncApi(options =>
{
    options.WithInfo("AsyncApiSolution.Api", "1.0.0")
           .WithDescription("API part of the AsyncAPI Solution")
           .IncludeXmlComments(typeof(SystemMessage).Assembly);
});

var app = builder.Build();

app.MapAsyncApi();
app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("v1", "AsyncApiSolution", "/asyncapi/v1.json");
});

app.MapGet("/", () => "AsyncAPI Solution API is running.");

app.Run();
