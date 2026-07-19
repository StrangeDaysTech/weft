using Weft.Server;
using Weft.Server.Auth;
using Weft.Server.Persistence;

// Example y-sync relay: serves a WebSocket endpoint compatible with Yjs clients (y-websocket/Tiptap) at
// ws://127.0.0.1:5199/collab/{docId}. See samples/tiptap-client for the client and the wire-compat check.
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("WEFT_SAMPLE_URLS") ?? "http://127.0.0.1:5199");

builder.Services.AddWeftServer();

// REQUIRED: without an IWeftAuthorizer, MapWeft fails at startup. This demo grants ReadWrite to everyone —
// a real consumer decides access from its own identity (JWT/cookies) via the HttpContext.
builder.Services.AddSingleton<IWeftAuthorizer, DemoAuthorizer>();

// Durable on-disk persistence (v1). Documents survive a server restart.
string dataDir = Path.Combine(AppContext.BaseDirectory, "weft-data");
builder.Services.AddSingleton<IDocumentStore>(new FileSystemDocumentStore(dataDir));

WebApplication app = builder.Build();
app.UseWebSockets();
app.MapWeft("/collab");

app.Logger.LogInformation("Weft sample relay on {Urls} — WebSocket endpoint /collab/{{docId}} — data in {DataDir}",
    string.Join(", ", app.Urls.DefaultIfEmpty("http://127.0.0.1:5199")), dataDir);
app.Run();

/// <summary>Demo authorizer: grants ReadWrite to every connection. Do NOT use in production.</summary>
internal sealed class DemoAuthorizer : IWeftAuthorizer
{
    public ValueTask<WeftAccess> AuthorizeAsync(HttpContext context, string docId, CancellationToken ct)
        => ValueTask.FromResult(WeftAccess.ReadWrite);
}
