namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Response DTO for OIDC application administration endpoints.
/// </summary>
/// <param name="ClientId">The client identifier.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Type">The application type (confidential, public).</param>
/// <param name="TenantId">The tenant identifier, or <see langword="null"/> for global applications.</param>
public sealed record AdminOidcApplicationResponse(
    string? ClientId,
    string? DisplayName,
    string? Type,
    Guid? TenantId);
