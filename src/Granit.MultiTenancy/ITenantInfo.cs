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
    /// ISO 3166 jurisdiction code for this tenant (e.g. <c>"FR"</c>, <c>"CA-QC"</c>, <c>"US-CA"</c>).
    /// Used by <c>IPrivacyRegulationResolver</c> to determine the applicable regulation(s).
    /// <c>null</c> when no jurisdiction is configured.
    /// </summary>
    string? Jurisdiction { get; }
}
