using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Scalar.Grpc;
using AsyncApiGrpc.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add gRPC
builder.Services.AddGrpc();

// Add AsyncAPI services with gRPC support
builder.Services.AddAsyncApi(options =>
{
    options.WithInfo("AsyncApiGrpc", "1.0.0")
           .WithDescription("A sample gRPC service with AsyncAPI documentation");
});

var app = builder.Build();

app.MapAsyncApi();
app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("v1", "AsyncApiGrpc", "/asyncapi/v1.json");
    // Enable the interactive gRPC console
    options.WithGrpcClient();
});

app.MapGrpcService<GreeterService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();
