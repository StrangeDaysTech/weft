using Microsoft.AspNetCore.Http;

namespace Weft.Server.Auth;

/// <summary>Access decision of a connection to a document (FR-019).</summary>
public enum WeftAccess
{
    /// <summary>No access: rejected with 403 before the WebSocket upgrade (no content byte travels).</summary>
    Deny,

    /// <summary>Read: receives sync/updates/awareness; if it sends a document update, it is closed with 1008.</summary>
    ReadOnly,

    /// <summary>Read and write: full y-sync flow.</summary>
    ReadWrite,
}

/// <summary>
/// Consumer's authorization hook (FR-019). Weft does not know users or parse tokens: the consumer
/// decides access with its own identity (JWT, cookies, headers…) from the <see cref="HttpContext"/> of
/// the upgrade request.
/// </summary>
/// <remarks>
/// The decision is <b>per-connection</b> (re-evaluated on each reconnection). Authorization is never optional nor
/// permissive-by-default: registering the server without an implementation of <see cref="IWeftAuthorizer"/> is an
/// explicit failure at startup (SC-010). The enforcement of the decision in the handshake (403 / 1008 close /
/// full flow) belongs to the connection handler (CHARTER-05); here only the contract is frozen.
/// </remarks>
public interface IWeftAuthorizer
{
    /// <summary>Decides the access of the incoming connection to document <paramref name="docId"/>.</summary>
    /// <param name="context">HTTP context of the upgrade request (identity, headers, cookies).</param>
    /// <param name="docId">Identifier of the requested document (last path segment, URL-decoded).</param>
    /// <param name="ct">Cancellation token of the request.</param>
    /// <returns>The granted access level.</returns>
    ValueTask<WeftAccess> AuthorizeAsync(HttpContext context, string docId, CancellationToken ct);
}
