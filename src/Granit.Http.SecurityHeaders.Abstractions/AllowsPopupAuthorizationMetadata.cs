namespace Granit.Http.SecurityHeaders;

/// <summary>
/// Marks an endpoint that opens a cross-origin popup and polls its
/// <c>window.location</c> to retrieve an OAuth2 authorization code (the
/// "popup + opener.postMessage / polling" pattern used by Scalar's
/// interactive Authorize button, among others).
/// </summary>
/// <remarks>
/// <para>
/// When this metadata is attached to the matched endpoint, the
/// security-headers middleware downgrades
/// <c>Cross-Origin-Opener-Policy</c> to <c>unsafe-none</c> for that
/// endpoint only. The framework default (<c>same-origin</c>) severs the
/// opener↔popup window reference the moment the popup navigates
/// cross-origin to the IdP, breaking polling-based OAuth flows even after
/// CSP <c>connect-src</c> is properly configured. In practice
/// <c>same-origin-allow-popups</c> is not sufficient with Scalar's
/// polling implementation — only <c>unsafe-none</c> reliably preserves
/// the window reference.
/// </para>
/// <para>
/// Scope is strictly per-endpoint: every other route keeps the strict
/// COOP baseline. The marker is meant for interactive doc / SPA UIs that
/// genuinely need the relaxation; it should not be attached to
/// JSON/data endpoints.
/// </para>
/// </remarks>
public sealed class AllowsPopupAuthorizationMetadata
{
    /// <summary>Shared stateless marker instance.</summary>
    public static AllowsPopupAuthorizationMetadata Instance { get; } = new();
}
