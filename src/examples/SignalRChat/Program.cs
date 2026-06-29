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

    // Channel binding describes the hub itself. It is attached to the hub via
    // [Channel("chatHub", BindingsRef = "chatHub")] on ChatHub.
    options.AddSignalRChannelBinding("chatHub", channel =>
    {
        channel.Hub = HubPath;
        channel.Transports = new List<string> { SignalRProtocol.Transports.WebSockets, SignalRProtocol.Transports.LongPolling };
        channel.Protocols = new List<string> { SignalRProtocol.HubProtocols.Json, SignalRProtocol.HubProtocols.MessagePack };
    });

    // --- Client-to-server hub methods --------------------------------------------------------
    // Operation bindings are attached to each hub method via [PublishOperation(BindingsRef = ...)].
    // The call type matters: it tells a client whether to `send` (fire-and-forget, no result),
    // `invoke` (await a result) or open a stream.

    // Fire-and-forget broadcasts/notifications: return Task with no result -> `send`.
    options.AddSignalROperationBinding("sendMessage", op => Bind(op, "SendMessage", SignalRProtocol.CallTypes.Send));
    options.AddSignalROperationBinding("sendToRoom", op => Bind(op, "SendToRoom", SignalRProtocol.CallTypes.Send));
    options.AddSignalROperationBinding("joinRoom", op => Bind(op, "JoinRoom", SignalRProtocol.CallTypes.Send));
    options.AddSignalROperationBinding("leaveRoom", op => Bind(op, "LeaveRoom", SignalRProtocol.CallTypes.Send));
    options.AddSignalROperationBinding("notifyTyping", op => Bind(op, "NotifyTyping", SignalRProtocol.CallTypes.Send));

    // Request/response: returns Task<T> the caller awaits -> `invocation`.
    options.AddSignalROperationBinding("getOnlineUsers", op => Bind(op, "GetOnlineUsers", SignalRProtocol.CallTypes.Invocation));

    // Streaming: returns IAsyncEnumerable<T> -> `streamInvocation`.
    options.AddSignalROperationBinding("streamHistory", op =>
    {
        Bind(op, "StreamHistory", SignalRProtocol.CallTypes.StreamInvocation);
        op.Streaming = true;
    });

    // --- Server-to-client pushes -------------------------------------------------------------
    // These map to the IChatClient methods (documented as `subscribe`/`send` operations). The
    // direction is what matters here; the client listens for them rather than calling them.
    options.AddSignalROperationBinding("receiveMessage", op => Push(op, "ReceiveMessage"));
    options.AddSignalROperationBinding("userJoined", op => Push(op, "UserJoined"));
    options.AddSignalROperationBinding("userLeft", op => Push(op, "UserLeft"));
    options.AddSignalROperationBinding("typingChanged", op => Push(op, "TypingChanged"));
});

var app = builder.Build();

app.UseRouting();

// Serve the browser chat client from wwwroot (GET / -> wwwroot/index.html).
app.UseDefaultFiles();
app.UseStaticFiles();

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

// Configures a client-to-server operation binding for a hub method.
static void Bind(SignalROperationBinding binding, string target, string callType)
{
    binding.Target = target;
    binding.Direction = SignalRProtocol.Directions.ClientToServer;
    binding.CallType = callType;
}

// Configures a server-to-client push operation binding. The direction conveys that the client
// receives the message rather than invoking it, so no call type is set.
static void Push(SignalROperationBinding binding, string target)
{
    binding.Target = target;
    binding.Direction = SignalRProtocol.Directions.ServerToClient;
}

// Exposed so integration tests can host this app with WebApplicationFactory<Program>.
public partial class Program;
