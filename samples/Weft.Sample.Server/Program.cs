using Weft.Server;
using Weft.Server.Auth;
using Weft.Server.Persistence;

// Relay y-sync de ejemplo: sirve un endpoint WebSocket compatible con clientes Yjs (y-websocket/Tiptap) en
// ws://127.0.0.1:5199/collab/{docId}. Ver samples/tiptap-client para el cliente y la validación de compat.
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("WEFT_SAMPLE_URLS") ?? "http://127.0.0.1:5199");

builder.Services.AddWeftServer();

// OBLIGATORIO: sin IWeftAuthorizer, MapWeft falla al arrancar. Este demo concede ReadWrite a todos —
// un consumidor real decide con su propia identidad (JWT/cookies) a partir del HttpContext.
builder.Services.AddSingleton<IWeftAuthorizer, DemoAuthorizer>();

// Persistencia durable en disco (v1). Los documentos sobreviven al reinicio del servidor.
string dataDir = Path.Combine(AppContext.BaseDirectory, "weft-data");
builder.Services.AddSingleton<IDocumentStore>(new FileSystemDocumentStore(dataDir));

WebApplication app = builder.Build();
app.UseWebSockets();
app.MapWeft("/collab");

app.Logger.LogInformation("Weft sample relay en {Urls} — endpoint WebSocket /collab/{{docId}} — datos en {DataDir}",
    string.Join(", ", app.Urls.DefaultIfEmpty("http://127.0.0.1:5199")), dataDir);
app.Run();

/// <summary>Authorizer de demostración: concede ReadWrite a toda conexión. NO usar en producción.</summary>
internal sealed class DemoAuthorizer : IWeftAuthorizer
{
    public ValueTask<WeftAccess> AuthorizeAsync(HttpContext context, string docId, CancellationToken ct)
        => ValueTask.FromResult(WeftAccess.ReadWrite);
}
