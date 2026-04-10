namespace Granit.MultiTenancy.Url;

/// <summary>
/// Lightweight DTO cached in-memory for URL resolution.
/// Contains only the fields needed to compute outbound tenant URLs.
/// </summary>
/// <param name="Identifier">Tenant slug/subdomain identifier (e.g., <c>"acme"</c>).</param>
/// <param name="CustomDomain">Optional custom domain (e.g., <c>"app.acme-corp.com"</c>).</param>
internal sealed record TenantUrlData(string Identifier, string? CustomDomain);
