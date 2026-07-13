using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Weft.Server.Auth;
using Weft.Server.Persistence;
using Weft.Versioning.Blobs;

namespace Weft.Server;

/// <summary>Registro (DI) y mapeo del endpoint del relay <see cref="WeftServer"/>.</summary>
public static class WeftServerExtensions
{
    /// <summary>
    /// Registra el relay en el contenedor. El consumidor DEBE registrar también un
    /// <see cref="IWeftAuthorizer"/> (obligatorio, se valida en <see cref="MapWeft"/> al arrancar) y un
    /// <see cref="IDocumentStore"/>; un <see cref="IBlobStore"/> es opcional (solo para
    /// <see cref="IWeftServer.PublishAsync"/>).
    /// </summary>
    public static IServiceCollection AddWeftServer(this IServiceCollection services, Action<WeftServerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new WeftServerOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddSingleton<WeftServer>(sp => new WeftServer(
            sp.GetRequiredService<WeftServerOptions>(),
            sp.GetRequiredService<IDocumentStore>(),
            sp.GetService<IBlobStore>()));
        services.AddSingleton<IWeftServer>(sp => sp.GetRequiredService<WeftServer>());

        return services;
    }

    /// <summary>
    /// Mapea el endpoint WebSocket del relay en <c>{pattern}/{docId}</c>. Falla al arrancar si no hay un
    /// <see cref="IWeftAuthorizer"/> registrado (la autorización nunca es opcional; SC-010). Semántica del
    /// handshake: <see cref="WeftAccess.Deny"/> → 403 antes del upgrade (0 bytes de contenido); en otro caso,
    /// upgrade y relay con el nivel de acceso concedido.
    /// </summary>
    public static IEndpointConventionBuilder MapWeft(this IEndpointRouteBuilder endpoints, string pattern)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        var probe = endpoints.ServiceProvider.GetService<IServiceProviderIsService>();
        if (probe is not null && !probe.IsService(typeof(IWeftAuthorizer)))
        {
            throw new InvalidOperationException(
                "AddWeftServer requiere registrar un IWeftAuthorizer antes de MapWeft: la autorización nunca es " +
                "opcional ni por-defecto-permisiva (SC-010).");
        }

        WeftServer server = endpoints.ServiceProvider.GetRequiredService<WeftServer>();
        string route = $"{pattern.TrimEnd('/')}/{{docId}}";

        return endpoints.Map(route, async (HttpContext context, string docId) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            IWeftAuthorizer authorizer = context.RequestServices.GetRequiredService<IWeftAuthorizer>();
            WeftAccess access = await authorizer.AuthorizeAsync(context, docId, context.RequestAborted);
            if (access == WeftAccess.Deny)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden; // antes del upgrade: 0 bytes de contenido
                return;
            }

            using WebSocket ws = await context.WebSockets.AcceptWebSocketAsync();
            await server.HandleConnectionAsync(docId, access, ws, context.RequestAborted);
        });
    }
}
