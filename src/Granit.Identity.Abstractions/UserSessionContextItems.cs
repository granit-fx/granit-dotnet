namespace Granit.Identity;

/// <summary>
/// Well-known <see cref="System.Collections.Generic.IDictionary{TKey,TValue}">HttpContext.Items</see> keys
/// carrying the identifier of the request's own session, resolved by the session-bearing middleware.
/// </summary>
/// <remarks>
/// The canonical <c>/sessions</c> endpoints flag and protect the current session by its id. When the BFF is the
/// session backend, that id is the BFF cookie session id (the <c>IBffTokenStore</c> key) — a different identifier
/// space from the OIDC <c>sid</c> claim, and one the downstream access token never carries. The in-process BFF
/// token-injection middleware already resolves it from the cookie, so it stashes it here for the endpoint to read,
/// letting the endpoint identify "current" without referencing the BFF cookie machinery. Absent for token-only
/// backends (OpenIddict, Keycloak), where the endpoint falls back to the <c>sid</c> claim.
/// </remarks>
public static class UserSessionContextItems
{
    /// <summary>Item key for the id of the request's own session in the active session backend's identifier space.</summary>
    public const string CurrentSessionId = "Granit:UserSession:CurrentSessionId";
}
