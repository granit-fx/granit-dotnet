namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Response DTO for OIDC scope administration endpoints.
/// </summary>
/// <param name="Name">The scope name.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="Resources">Resource server identifiers associated with this scope.</param>
public sealed record AdminOidcScopeResponse(
    string? Name,
    string? DisplayName,
    string? Description,
    string[] Resources);
