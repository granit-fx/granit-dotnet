namespace Granit.MultiTenancy.Endpoints.Dtos;

/// <summary>
/// Request to create a new tenant.
/// </summary>
/// <param name="Name">Display name of the tenant (max 256 characters).</param>
/// <param name="Identifier">Unique slug/subdomain identifier (max 64 characters, lowercase alphanumeric + hyphens).</param>
/// <param name="ContactEmail">Optional contact email address.</param>
/// <param name="Jurisdiction">ISO 3166 jurisdiction code (e.g. <c>"FR"</c>, <c>"CA-QC"</c>, <c>"US-CA"</c>). Omit or pass <c>null</c> to leave unconfigured.</param>
public sealed record CreateTenantRequest(
    string Name,
    string Identifier,
    string? ContactEmail = null,
    string? Jurisdiction = null);
