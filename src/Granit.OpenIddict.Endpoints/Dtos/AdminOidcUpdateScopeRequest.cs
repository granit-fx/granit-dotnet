namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Request DTO for updating an existing OIDC scope.
/// </summary>
/// <param name="DisplayName">A human-readable display name. Null leaves unchanged.</param>
/// <param name="Description">An optional human-readable description. Null leaves unchanged.</param>
public sealed record AdminOidcUpdateScopeRequest(
    string? DisplayName = null,
    string? Description = null);
