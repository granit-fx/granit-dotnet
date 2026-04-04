using Granit.Bff.Options;

namespace Granit.Bff;

/// <summary>
/// Orchestrates the BFF logout flow: token revocation (RFC 7009), session cleanup,
/// and OIDC end_session URL construction.
/// </summary>
public interface IBffLogoutOrchestrator
{
    /// <summary>
    /// Revokes tokens at the authorization server (best-effort), removes the session
    /// from the distributed cache, and returns the ID token hint for the end_session URL.
    /// </summary>
    /// <param name="frontendName">The frontend name (e.g., "admin").</param>
    /// <param name="sessionId">The session identifier from the session cookie.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <c>id_token</c> hint if the session existed, or <c>null</c> if no session was found.</returns>
    Task<string?> RevokeSessionAsync(
        string frontendName,
        string sessionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Builds the OIDC end_session URL for redirecting the user after logout.
    /// </summary>
    /// <param name="frontend">The frontend configuration.</param>
    /// <param name="postLogoutRedirectUri">The absolute post-logout redirect URI.</param>
    /// <param name="idTokenHint">The ID token hint, or <c>null</c> if unavailable.</param>
    /// <returns>The fully constructed end_session URL.</returns>
    string BuildEndSessionUrl(
        BffFrontendOptions frontend,
        string postLogoutRedirectUri,
        string? idTokenHint);
}
