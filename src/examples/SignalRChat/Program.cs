using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;
using Bielu.AspNetCore.AsyncApi.Scalar.SignalR;
using Bielu.AspNetCore.AsyncApi.UI;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Authentication;
using Scalar.AspNetCore;
using SignalRChat;
using SignalRChat.Auth;

var builder = WebApplication.CreateBuilder(args);

const string HubPath = "/chatHub";
const string SecureHubPath = "/secureChatHub";

// 1. Register SignalR and the chat hubs.
builder.Services.AddSignalR();

// AsyncAPI document generation relies on MVC application parts for assembly scanning.
builder.Services.AddControllers();

// 2a. Register the API-key authentication scheme used by SecureChatHub.
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", options => { });
builder.Services.AddAuthorization();

// 2b. Register AsyncAPI generation for the public "signalr" document (ChatHub).
builder.Services.AddAsyncApi("signalr", options =>
{
    options.WithInfo("SignalR Chat", "1.0.0")
        .WithDescription("Example AsyncAPI document for an ASP.NET Core SignalR chat hub.");

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

    options.IncludeOnlyChannels.Add("chatHub");

    options.AddSignalRChannelBinding("chatHub", channel =>
    {
        channel.Hub = HubPath;
        channel.Transports = new List<string> { SignalRProtocol.Transports.WebSockets, SignalRProtocol.Transports.LongPolling };
        channel.Protocols = new List<string> { SignalRProtocol.HubProtocols.Json, SignalRProtocol.HubProtocols.MessagePack };
    });

    options.AddSignalROperationBinding("sendMessage", op => Bind(op, "SendMessage", SignalRProtocol.CallTypes.Send));
    options.AddSignalROperationBinding("sendToRoom", op => Bind(op, "SendToRoom", SignalRProtocol.CallTypes.Send));
    options.AddSignalROperationBinding("joinRoom", op => Bind(op, "JoinRoom", SignalRProtocol.CallTypes.Send));
    options.AddSignalROperationBinding("leaveRoom", op => Bind(op, "LeaveRoom", SignalRProtocol.CallTypes.Send));
    options.AddSignalROperationBinding("notifyTyping", op => Bind(op, "NotifyTyping", SignalRProtocol.CallTypes.Send));
    options.AddSignalROperationBinding("getOnlineUsers", op => Bind(op, "GetOnlineUsers", SignalRProtocol.CallTypes.Invocation));
    options.AddSignalROperationBinding("streamHistory", op =>
    {
        Bind(op, "StreamHistory", SignalRProtocol.CallTypes.StreamInvocation);
        op.Streaming = true;
    });

    options.AddSignalROperationBinding("receiveMessage", op => Push(op, "ReceiveMessage"));
    options.AddSignalROperationBinding("userJoined", op => Push(op, "UserJoined"));
    options.AddSignalROperationBinding("userLeft", op => Push(op, "UserLeft"));
    options.AddSignalROperationBinding("typingChanged", op => Push(op, "TypingChanged"));
});

// 2c. Register AsyncAPI generation for the secured "signalr-secure" document (SecureChatHub).
builder.Services.AddAsyncApi("signalr-secure", options =>
{
    options.WithInfo("SignalR Chat (Secured)", "1.0.0")
        .WithDescription(
            $"Example AsyncAPI document for a SignalR chat hub protected by API key authentication. " +
            $"Enter '{ApiKeyAuthenticationHandler.DemoApiKey}' as the API key in Scalar's Authentication panel.");

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

    options.IncludeOnlyChannels.Add("secureChatHub");

    options.AddSignalRChannelBinding("secureChatHub", channel =>
    {
        channel.Hub = SecureHubPath;
        channel.Transports = new List<string> { SignalRProtocol.Transports.WebSockets, SignalRProtocol.Transports.LongPolling };
        channel.Protocols = new List<string> { SignalRProtocol.HubProtocols.Json, SignalRProtocol.HubProtocols.MessagePack };
    });

    options.AddSignalROperationBinding("secureSendMessage", op => Bind(op, "SendMessage", SignalRProtocol.CallTypes.Send));
    options.AddSignalROperationBinding("secureGetOnlineUsers", op => Bind(op, "GetOnlineUsers", SignalRProtocol.CallTypes.Invocation));
    options.AddSignalROperationBinding("secureReceiveMessage", op => Push(op, "ReceiveMessage"));
    options.AddSignalROperationBinding("secureUserJoined", op => Push(op, "UserJoined"));
    options.AddSignalROperationBinding("secureUserLeft", op => Push(op, "UserLeft"));

    // Declare the API key security scheme so Scalar's Authentication UI surfaces it.
    // Use SecuritySchemeType.ApiKey (the AsyncAPI-standard type); ByteBard now correctly
    // serializes the `name` field for this type (fixed in AsyncAPI.NET).
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Components ??= new AsyncApiComponents();
        doc.Components.SecuritySchemes ??= new Dictionary<string, AsyncApiSecurityScheme>();
        doc.Components.SecuritySchemes["apiKey"] = new AsyncApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Query,
            Name = "api_key",
            Description = $"API key required to connect. Demo value: '{ApiKeyAuthenticationHandler.DemoApiKey}'.",
        };

        // Reference the scheme from the server so Scalar pre-selects it in the auth panel.
        // getAsyncApiDocumentSecurityRequirements() reads server.security to determine what
        // the document requires, which drives the default selection in the AuthSelector.
        if (doc.Servers?.ContainsKey("signalr") == true)
        {
            doc.Servers["signalr"].Security.Add(
                new AsyncApiSecuritySchemeReference("#/components/securitySchemes/apiKey"));
        }

        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Serve the browser chat client from wwwroot (GET / -> wwwroot/index.html).
app.UseDefaultFiles();
app.UseStaticFiles();

// 3. Map the hubs and the AsyncAPI document + UI endpoints.
app.MapHub<ChatHub>(HubPath);
app.MapHub<SecureChatHub>(SecureHubPath).RequireAuthorization();

app.MapAsyncApi();      // GET /asyncapi/signalr.json  +  /asyncapi/signalr-secure.json
app.MapAsyncApiUi();    // GET /asyncapi

// Serve the SignalR-enabled Scalar bundle (GET /scalar/signalr/plugin.js).
app.MapScalarSignalRAssets();

// Render both AsyncAPI documents with Scalar and wire the interactive SignalR console.
app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("signalr", "SignalR Chat", "/asyncapi/signalr.json");
    options.AddAsyncApiDocument("signalr-secure", "SignalR Chat (Secured)", "/asyncapi/signalr-secure.json");
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

// Configures a server-to-client push operation binding.
static void Push(SignalROperationBinding binding, string target)
{
    binding.Target = target;
    binding.Direction = SignalRProtocol.Directions.ServerToClient;
}

// Exposed so integration tests can host this app with WebApplicationFactory<Program>.
public partial class Program;
