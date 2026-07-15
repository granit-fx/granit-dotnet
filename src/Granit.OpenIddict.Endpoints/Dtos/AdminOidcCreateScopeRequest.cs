namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Request DTO for creating a new OIDC scope.
/// </summary>
/// <param name="Name">The scope name (unique).</param>
/// <param name="DisplayName">A human-readable display name.</param>
/// <param name="Description">An optional human-readable description.</param>
/// <param name="Resources">Resource server identifiers associated with this scope.</param>
/// <param name="TenantId">Owning tenant. Omit (or <see langword="null"/>) for a global scope, or to inherit the caller's active tenant. A host administrator may set this explicitly to provision a scope for a specific tenant; a tenant-scoped administrator may only target their own tenant (a mismatch is rejected with 403).</param>
public sealed record AdminOidcCreateScopeRequest(
    string Name,
    string? DisplayName = null,
    string? Description = null,
    string[]? Resources = null,
    Guid? TenantId = null);
