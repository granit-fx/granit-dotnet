namespace Granit.MultiTenancy;

/// <summary>
/// Tenant information.
/// </summary>
public interface ITenantInfo
{
    /// <summary>Unique tenant identifier.</summary>
    Guid? Id { get; }

    /// <summary>Tenant name.</summary>
    string? Name { get; }

    /// <summary>
    /// Unique slug or subdomain identifier for this tenant (e.g. <c>"acme-corp"</c>).
    /// Used for URL-based tenant resolution (subdomain, route segment).
    /// Null when tenant was resolved by ID only (e.g. header or JWT claim).
    /// </summary>
    string? Identifier { get; }

    /// <summary>
    /// Privacy regulation code or ISO country code for this tenant.
    /// Used by <c>IPrivacyRegulationResolver</c> to determine the applicable regulation.
    /// Null when no jurisdiction is configured.
    /// </summary>
    string? Jurisdiction { get; }
}
