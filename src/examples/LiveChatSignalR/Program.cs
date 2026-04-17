using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.UI;
using ByteBard.AsyncAPI.Bindings.WebSockets;
using LiveChatSignalR.Hubs;
using LiveChatSignalR.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// ── Authentication ───────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddAuthorization();

// ── Application services ─────────────────────────────────────────────────────
builder.Services.AddSingleton<InMemoryMessageStore>();

// ── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── AsyncAPI documentation ───────────────────────────────────────────────────
builder.Services.AddAsyncApi(options =>
{
    options.AddServer("websocket", "localhost:5000", "ws", server =>
    {
        server.Description = "WebSocket server — real-time live chat via SignalR";
    });

    options
        .WithInfo("Live Chat API", "1.0.0")
        .WithDefaultContentType("application/json")
        .WithDescription(
            "A live-chat service built with ASP.NET Core SignalR. " +
            "Clients connect over WebSocket to join chats, send messages, " +
            "and receive user-presence events in real time. " +
            "Group rooms and private conversations use the same channel.")
        .WithLicense("Apache 2.0", "https://www.apache.org/licenses/LICENSE-2.0");

    // Reusable WebSocket channel binding
    options.AddChannelBinding("wsChat", new WebSocketsChannelBinding());
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ── SignalR hub ──────────────────────────────────────────────────────────────
app.MapHub<ChatHub>("/hubs/chat");

// ── AsyncAPI endpoints ───────────────────────────────────────────────────────
app.MapAsyncApi();
app.MapAsyncApiUi();

app.MapControllers();

app.Run();
