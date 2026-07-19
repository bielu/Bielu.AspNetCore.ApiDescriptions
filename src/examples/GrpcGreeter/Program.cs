using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc;
using Bielu.AspNetCore.AsyncApi.Scalar.Grpc;
using Grpc.AspNetCore.Web;
using GrpcGreeter.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Register gRPC and the greeter service.
builder.Services.AddGrpc();

// AsyncAPI document generation relies on MVC application parts for assembly scanning.
builder.Services.AddControllers();

// 2. Register AsyncAPI generation for the "grpc" document and describe the gRPC protocol.
builder.Services.AddAsyncApi("grpc", options =>
{
    options.WithInfo("gRPC Greeter", "1.0.0")
        .WithDescription("Example AsyncAPI document for an ASP.NET Core gRPC greeter service.");

    // A gRPC server advertising the services it hosts and the capabilities it negotiates.
    options.AddServer("grpc", "localhost:5001", GrpcProtocol.ProtocolName, server =>
    {
        server.Description = "Local gRPC endpoint";
        server.Bindings = new ByteBard.AsyncAPI.Models.AsyncApiBindings<ByteBard.AsyncAPI.Models.Interfaces.IServerBinding>
        {
            new GrpcServerBinding
            {
                Services = { "greet.Greeter" },
                Reflection = true,
                Tls = true,
                Compressions = { GrpcProtocol.Compressions.Gzip, GrpcProtocol.Compressions.Identity },
            },
        };
    });

    // Channel + operation bindings are attached to the service via [Channel(BindingsRef=...)] /
    // [PublishOperation(BindingsRef=...)] on GreeterService.
    options.AddGrpcChannelBinding("greeter", channel =>
    {
        channel.Service = "greet.Greeter";
        channel.Package = "greet";
        channel.ProtoFile = "Protos/greet.proto";
    });

    options.AddGrpcOperationBinding("sayHello", operation =>
    {
        operation.Method = "SayHello";
        operation.MethodType = GrpcMethodType.Unary;
        operation.RequestType = "greet.HelloRequest";
        operation.ResponseType = "greet.HelloReply";
        operation.IdempotencyLevel = GrpcProtocol.IdempotencyLevels.NoSideEffects;
        operation.DeadlineSeconds = 30;
    });

    options.AddGrpcOperationBinding("sayHellos", operation =>
    {
        operation.Method = "SayHellos";
        operation.MethodType = GrpcMethodType.ServerStreaming;
        operation.RequestType = "greet.HelloRequest";
        operation.ResponseType = "greet.HelloReply";
    });
});

var app = builder.Build();

app.UseRouting();

// 3. Enable gRPC-Web for every gRPC service — browsers cannot speak native gRPC (no HTTP/2
// trailers), so the interactive Scalar console calls the service over gRPC-Web.
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

// 4. Map the gRPC service and the AsyncAPI document endpoint.
app.MapGrpcService<GreeterService>();
app.MapAsyncApi();      // GET /asyncapi/grpc.json

// Serve the gRPC-enabled Scalar bundle + protobuf descriptors (GET /bielu/scalar/grpc/plugin.js,
// GET /bielu/scalar/grpc/descriptors).
app.MapScalarGrpcAssets();

// Render the generated AsyncAPI document with Scalar (served at /scalar) and wire the
// interactive gRPC console.
app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("grpc", "gRPC Greeter", "/asyncapi/grpc.json");
    options.WithGrpcClient();
});

app.Run();

// Exposed so integration tests can host this app with WebApplicationFactory<Program>.
public partial class Program;
