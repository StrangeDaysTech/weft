using Microsoft.AspNetCore.Http;

namespace Weft.Server.Auth;

/// <summary>Decisión de acceso de una conexión a un documento (FR-019).</summary>
public enum WeftAccess
{
    /// <summary>Sin acceso: se rechaza con 403 antes del upgrade WebSocket (ningún byte de contenido viaja).</summary>
    Deny,

    /// <summary>Lectura: recibe sync/updates/awareness; si envía un update de documento, se cierra con 1008.</summary>
    ReadOnly,

    /// <summary>Lectura y escritura: flujo y-sync completo.</summary>
    ReadWrite,
}

/// <summary>
/// Hook de autorización del consumidor (FR-019). Weft no conoce usuarios ni parsea tokens: el consumidor
/// decide el acceso con su propia identidad (JWT, cookies, headers…) a partir del <see cref="HttpContext"/> de
/// la petición de upgrade.
/// </summary>
/// <remarks>
/// La decisión es <b>por-conexión</b> (se re-evalúa en cada reconexión). La autorización nunca es opcional ni
/// por-defecto-permisiva: registrar el servidor sin una implementación de <see cref="IWeftAuthorizer"/> es un
/// fallo explícito al arrancar (SC-010). El enforcement de la decisión en el handshake (403 / cierre 1008 /
/// flujo completo) pertenece al connection handler (CHARTER-05); aquí se congela solo el contrato.
/// </remarks>
public interface IWeftAuthorizer
{
    /// <summary>Decide el acceso de la conexión entrante al documento <paramref name="docId"/>.</summary>
    /// <param name="context">Contexto HTTP de la petición de upgrade (identidad, headers, cookies).</param>
    /// <param name="docId">Identificador del documento solicitado (último segmento de la ruta, URL-decoded).</param>
    /// <param name="ct">Token de cancelación de la petición.</param>
    /// <returns>El nivel de acceso concedido.</returns>
    ValueTask<WeftAccess> AuthorizeAsync(HttpContext context, string docId, CancellationToken ct);
}
