using Bielu.AspNetCore.AsyncApi.Extensions;
using AsyncApiSolution.Contracts;
using AsyncApiSolution.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add AsyncAPI services
builder.Services.AddAsyncApi(options =>
{
    options.WithInfo("AsyncApiSolution.Worker", "1.0.0")
           .WithDescription("Worker part of the AsyncAPI Solution")
           .IncludeXmlComments(typeof(SystemMessage).Assembly);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();
