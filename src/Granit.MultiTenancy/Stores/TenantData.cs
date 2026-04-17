namespace Granit.MultiTenancy.Stores;

/// <summary>
/// Read model for tenant administration.
/// Returned by <see cref="ITenantReader"/> for admin CRUD operations.
/// Distinct from <see cref="TenantInfo"/> which is the lightweight resolution DTO.
/// </summary>
/// <param name="Id">Unique tenant identifier.</param>
/// <param name="Name">Display name of the tenant.</param>
/// <param name="Identifier">Unique slug/subdomain identifier.</param>
/// <param name="ContactEmail">Optional contact email address.</param>
/// <param name="Activated">Whether the tenant is active.</param>
/// <param name="Jurisdiction">Privacy regulation code or ISO country code, or <c>null</c>.</param>
/// <param name="CreatedAt">Timestamp when the tenant was created.</param>
/// <param name="CustomDomain">Optional custom domain for outbound URL generation (e.g., <c>"app.acme-corp.com"</c>).</param>
public sealed record TenantData(
    Guid Id,
    string Name,
    string Identifier,
    string? ContactEmail,
    bool Activated,
    string? Jurisdiction,
    DateTimeOffset CreatedAt,
    string? CustomDomain = null);
