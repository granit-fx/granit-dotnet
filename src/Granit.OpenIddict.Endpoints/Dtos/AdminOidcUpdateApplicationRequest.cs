namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Request DTO for updating an existing OIDC application.
/// </summary>
/// <param name="DisplayName">A human-readable display name.</param>
/// <param name="Type">The application type (web, native). Null leaves unchanged.</param>
public sealed record AdminOidcUpdateApplicationRequest(
    string? DisplayName = null,
    string? Type = null);
