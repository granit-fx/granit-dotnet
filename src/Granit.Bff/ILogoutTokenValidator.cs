namespace Granit.Bff;

/// <summary>
/// Validates OIDC Back-Channel Logout tokens: JWT signature verification against
/// the IdP's JWKS, issuer, audience, and event claim validation.
/// </summary>
public interface ILogoutTokenValidator
{
    /// <summary>
    /// Validates the logout token's signature against the IdP's JWKS and checks claims.
    /// </summary>
    /// <param name="logoutToken">The raw JWT string.</param>
    /// <param name="expectedClientId">Expected audience (client_id of this relying party).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validated claims, or <c>null</c> if validation fails.</returns>
    Task<ValidatedLogoutToken?> ValidateAsync(
        string logoutToken, string expectedClientId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Claims from a validated OIDC back-channel logout token.
/// </summary>
public sealed record ValidatedLogoutToken(
    string? Issuer,
    string? Subject,
    string? Jti,
    bool HasBackChannelLogoutEvent)
{
    /// <summary>Audience claim values (can be a single string or array in JWT).</summary>
    public IReadOnlyList<string>? Audiences { get; init; }
}
