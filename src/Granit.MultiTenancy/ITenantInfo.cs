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
}
