using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Grpc.Core;

namespace GrpcGreeter.Services;

/// <summary>
/// A real ASP.NET Core gRPC service. It is also annotated with AsyncAPI attributes so the document
/// generator surfaces it as the <c>greeter</c> channel; the gRPC protocol bindings are linked to that
/// channel/operation via the <c>BindingsRef</c> values registered in <c>Program.cs</c>.
/// </summary>
[AsyncApi]
[Channel("greeter", BindingsRef = "greeter", Description = "Greeting service backed by ASP.NET Core gRPC.")]
public class GreeterService : Greeter.GreeterBase
{
    /// <summary>Unary RPC: a client sends a name and receives a single greeting.</summary>
    [PublishOperation(typeof(HelloRequest), "greet", Summary = "Send a greeting request.", BindingsRef = "sayHello")]
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        => Task.FromResult(new HelloReply { Message = $"Hello {request.Name}" });

    /// <summary>Server-streaming RPC: a client sends a name and receives a stream of greetings.</summary>
    [PublishOperation(typeof(HelloRequest), "greet", Summary = "Stream greeting responses.", BindingsRef = "sayHellos")]
    public override async Task SayHellos(HelloRequest request, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
    {
        for (var i = 0; i < 3; i++)
        {
            await responseStream.WriteAsync(new HelloReply { Message = $"Hello {request.Name} ({i + 1})" });
        }
    }
}
