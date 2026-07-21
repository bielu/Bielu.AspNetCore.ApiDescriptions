using Bielu.AspNetCore.AsyncApi.Extensions;
using AsyncApiConsole;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add AsyncAPI services
builder.Services.AddAsyncApi(options =>
{
    options.WithInfo("AsyncApiConsole", "1.0.0")
           .WithDescription("A console application with AsyncAPI documentation")
           .IncludeXmlComments(typeof(Program).Assembly);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();
