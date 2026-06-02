namespace Granit.MultiTenancy.Endpoints.Dtos;

/// <summary>
/// Response representing a tenant.
/// </summary>
/// <param name="Id">Unique tenant identifier.</param>
/// <param name="Name">Display name of the tenant.</param>
/// <param name="Identifier">Unique slug/subdomain identifier.</param>
/// <param name="ContactEmail">Optional contact email address.</param>
/// <param name="Activated">Whether the tenant is active.</param>
/// <param name="Jurisdiction">Privacy regulation code or ISO country code, or <c>null</c>.</param>
/// <param name="CreatedAt">Timestamp when the tenant was created.</param>
/// <param name="ConcurrencyStamp">Opaque optimistic-concurrency token. Pass back in update requests to detect concurrent modifications (HTTP 409).</param>
public sealed record TenantResponse(
    Guid Id,
    string Name,
    string Identifier,
    string? ContactEmail,
    bool Activated,
    string? Jurisdiction,
    DateTimeOffset CreatedAt,
    string ConcurrencyStamp);
