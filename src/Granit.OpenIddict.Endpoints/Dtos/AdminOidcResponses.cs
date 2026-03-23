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

/// <summary>
/// Response DTO for OIDC scope administration endpoints.
/// </summary>
/// <param name="Name">The scope name.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Description">Human-readable description.</param>
public sealed record AdminOidcScopeResponse(
    string? Name,
    string? DisplayName,
    string? Description);

/// <summary>
/// Response DTO for OIDC authorization administration endpoints.
/// </summary>
/// <param name="Id">The authorization identifier.</param>
/// <param name="Subject">The subject (user) identifier.</param>
/// <param name="ClientId">The client identifier.</param>
/// <param name="Status">The authorization status.</param>
/// <param name="Type">The authorization type (permanent, ad-hoc).</param>
public sealed record AdminOidcAuthorizationResponse(
    Guid Id,
    string? Subject,
    string? ClientId,
    string? Status,
    string? Type);
