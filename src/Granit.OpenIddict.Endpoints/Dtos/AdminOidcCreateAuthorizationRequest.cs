namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Request DTO for creating a permanent OIDC authorization (consent grant).
/// </summary>
/// <param name="Subject">The user identifier (subject claim) granting consent.</param>
/// <param name="ClientId">The OIDC client identifier the consent applies to.</param>
/// <param name="Scopes">The scopes the user has consented to.</param>
public sealed record AdminOidcCreateAuthorizationRequest(
    string Subject,
    string ClientId,
    IReadOnlyList<string> Scopes);
