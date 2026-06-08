namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Request DTO for creating a new OIDC scope.
/// </summary>
/// <param name="Name">The scope name (unique).</param>
/// <param name="DisplayName">A human-readable display name.</param>
/// <param name="Description">An optional human-readable description.</param>
/// <param name="Resources">Resource server identifiers associated with this scope.</param>
public sealed record AdminOidcCreateScopeRequest(
    string Name,
    string? DisplayName = null,
    string? Description = null,
    string[]? Resources = null);
