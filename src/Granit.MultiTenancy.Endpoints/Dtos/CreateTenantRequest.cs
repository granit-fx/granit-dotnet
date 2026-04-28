namespace Granit.MultiTenancy.Endpoints.Dtos;

/// <summary>
/// Request to create a new tenant.
/// </summary>
/// <param name="Name">Display name of the tenant (max 256 characters).</param>
/// <param name="Identifier">Unique slug/subdomain identifier (max 64 characters, lowercase alphanumeric + hyphens).</param>
/// <param name="ContactEmail">Optional contact email address.</param>
/// <param name="Jurisdiction">Privacy regulation code or ISO country code (e.g. <c>"BE"</c>, <c>"FR"</c>), or <c>null</c>.</param>
public sealed record CreateTenantRequest(
    string Name,
    string Identifier,
    string? ContactEmail,
    string? Jurisdiction);
