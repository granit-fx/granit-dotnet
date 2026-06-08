namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Request DTO for updating an existing OIDC scope.
/// </summary>
/// <param name="DisplayName">A human-readable display name. Null leaves unchanged.</param>
/// <param name="Description">An optional human-readable description. Null leaves unchanged.</param>
/// <param name="Resources">Resource server identifiers. Null leaves unchanged; empty array clears all.</param>
public sealed record AdminOidcUpdateScopeRequest(
    string? DisplayName = null,
    string? Description = null,
    string[]? Resources = null);
