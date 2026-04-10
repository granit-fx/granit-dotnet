namespace Granit.MultiTenancy;

/// <summary>
/// Strategy for generating outbound tenant-specific URLs (email links, templates, etc.).
/// </summary>
public enum TenantUrlStrategy
{
    /// <summary>
    /// Shared URL for all tenants (default, backward-compatible).
    /// Uses the static fallback base URL from configuration.
    /// </summary>
    Shared,

    /// <summary>
    /// Subdomain per tenant, derived from the domain template.
    /// Example: domain template <c>"{0}.example.com"</c> + tenant identifier <c>"acme"</c>
    /// → <c>https://acme.example.com</c>.
    /// </summary>
    Subdomain,

    /// <summary>
    /// Custom domain per tenant, stored in the <c>Tenant.CustomDomain</c> property.
    /// Falls back to the static base URL when no custom domain is configured.
    /// </summary>
    CustomDomain,

    /// <summary>
    /// Hybrid mode (industry standard): uses the tenant's custom domain when set,
    /// otherwise derives the URL from the domain template.
    /// This is the recommended strategy for production SaaS deployments.
    /// </summary>
    Hybrid,
}
