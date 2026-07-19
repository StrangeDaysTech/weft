using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Weft.Server.Auth;
using Weft.Server.Persistence;
using Weft.Versioning.Blobs;

namespace Weft.Server;

/// <summary>Registration (DI) and endpoint mapping of the <see cref="WeftServer"/> relay.</summary>
public static class WeftServerExtensions
{
    /// <summary>
    /// Registers the relay in the container. The consumer MUST also register an
    /// <see cref="IWeftAuthorizer"/> (mandatory, validated in <see cref="MapWeft"/> at startup) and an
    /// <see cref="IDocumentStore"/>; an <see cref="IBlobStore"/> is optional (only for
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
    /// Maps the relay's WebSocket endpoint at <c>{pattern}/{docId}</c>. Fails at startup if there is no
    /// <see cref="IWeftAuthorizer"/> registered (authorization is never optional; SC-010). Handshake
    /// semantics: <see cref="WeftAccess.Deny"/> → 403 before the upgrade (0 bytes of content); otherwise,
    /// upgrade and relay with the granted access level.
    /// </summary>
    public static IEndpointConventionBuilder MapWeft(this IEndpointRouteBuilder endpoints, string pattern)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        var probe = endpoints.ServiceProvider.GetService<IServiceProviderIsService>();
        if (probe is not null)
        {
            if (!probe.IsService(typeof(IWeftAuthorizer)))
            {
                throw new InvalidOperationException(
                    "AddWeftServer requires registering an IWeftAuthorizer before MapWeft: authorization is never " +
                    "optional nor permissive-by-default (SC-010).");
            }

            if (!probe.IsService(typeof(IDocumentStore)))
            {
                throw new InvalidOperationException(
                    "AddWeftServer requires registering an IDocumentStore before MapWeft " +
                    "(InMemoryDocumentStore/FileSystemDocumentStore or an EFCore/Redis adapter).");
            }
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
                context.Response.StatusCode = StatusCodes.Status403Forbidden; // before the upgrade: 0 bytes of content
                return;
            }

            using WebSocket ws = await context.WebSockets.AcceptWebSocketAsync();
            await server.HandleConnectionAsync(docId, access, ws, context.RequestAborted);
        });
    }
}
