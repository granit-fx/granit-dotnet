namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Response DTO for OIDC authorization administration endpoints.
/// </summary>
/// <param name="Id">The authorization identifier.</param>
/// <param name="Subject">The subject (user) identifier.</param>
/// <param name="ClientId">The client identifier.</param>
/// <param name="Status">The authorization status.</param>
/// <param name="Type">The authorization type (permanent, ad-hoc).</param>
/// <param name="Scopes">The granted scopes.</param>
public sealed record AdminOidcAuthorizationResponse(
    Guid Id,
    string? Subject,
    string? ClientId,
    string? Status,
    string? Type,
    string[] Scopes);
