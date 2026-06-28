using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;
using Bielu.AspNetCore.AsyncApi.Scalar.SignalR;
using Bielu.AspNetCore.AsyncApi.UI;
using Scalar.AspNetCore;
using SignalRChat;

var builder = WebApplication.CreateBuilder(args);

const string HubPath = "/chatHub";

// 1. Register SignalR and the chat hub.
builder.Services.AddSignalR();

// AsyncAPI document generation relies on MVC application parts for assembly scanning.
builder.Services.AddControllers();

// 2. Register AsyncAPI generation for the "signalr" document and describe the SignalR protocol.
builder.Services.AddAsyncApi("signalr", options =>
{
    options.WithInfo("SignalR Chat", "1.0.0")
        .WithDescription("Example AsyncAPI document for an ASP.NET Core SignalR chat hub.");

    // A SignalR server advertising the transports and hub protocols it supports.
    options.AddServer("signalr", "localhost:5000", SignalRProtocol.ProtocolName, server =>
    {
        server.Description = "Local SignalR endpoint";
        server.Bindings = new ByteBard.AsyncAPI.Models.AsyncApiBindings<ByteBard.AsyncAPI.Models.Interfaces.IServerBinding>
        {
            new SignalRServerBinding
            {
                Transports = { SignalRProtocol.Transports.WebSockets, SignalRProtocol.Transports.LongPolling },
                Protocols = { SignalRProtocol.HubProtocols.Json, SignalRProtocol.HubProtocols.MessagePack },
            },
        };
    });

    // Channel + operation bindings are attached to the hub via [Channel(BindingsRef=...)] /
    // [PublishOperation(BindingsRef=...)] on ChatHub, exactly like any other protocol binding, e.g.
    //   options.AddChannelBinding("ws", new WebSocketsChannelBinding());
    options.AddChannelBinding("chatHub", new SignalRChannelBinding
    {
        Hub = HubPath,
        Transports = { SignalRProtocol.Transports.WebSockets, SignalRProtocol.Transports.LongPolling },
        Protocols = { SignalRProtocol.HubProtocols.Json, SignalRProtocol.HubProtocols.MessagePack },
    });

    options.AddOperationBinding("sendMessage", new SignalROperationBinding
    {
        Target = "SendMessage",
        Direction = SignalRProtocol.Directions.ClientToServer,
        CallType = SignalRProtocol.CallTypes.Invocation,
    });
});

var app = builder.Build();

app.UseRouting();

// 3. Map the hub and the AsyncAPI document + UI endpoints.
app.MapHub<ChatHub>(HubPath);
app.MapAsyncApi();      // GET /asyncapi/signalr.json
app.MapAsyncApiUi();    // GET /asyncapi

// Serve the SignalR-enabled Scalar bundle (GET /scalar/signalr/bundle.js).
app.MapScalarSignalRAssets();

// Render the generated AsyncAPI document with Scalar (served at /scalar), and add the
// interactive SignalR console. The console discovers the AsyncAPI document automatically from
// Scalar's own sources — no need to declare it again.
app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("signalr", "SignalR Chat", "/asyncapi/signalr.json");
    options.WithSignalRClient();
});

app.Run();

// Exposed so integration tests can host this app with WebApplicationFactory<Program>.
public partial class Program;
