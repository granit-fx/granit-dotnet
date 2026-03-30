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
    /// Privacy regulation code or ISO country code for this tenant.
    /// Used by <c>IPrivacyRegulationResolver</c> to determine the applicable regulation.
    /// Null when no jurisdiction is configured.
    /// </summary>
    string? Jurisdiction { get; }
}
