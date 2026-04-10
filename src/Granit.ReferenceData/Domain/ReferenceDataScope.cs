namespace Granit.ReferenceData.Domain;

/// <summary>
/// Defines the multi-tenancy scope of a reference data type.
/// Controls query filtering, unique constraints, cache isolation, and admin authorization.
/// </summary>
public enum ReferenceDataScope
{
    /// <summary>
    /// Data shared across all tenants, managed by the host administrator.
    /// <c>TenantId</c> is always <see langword="null"/>. Unique constraint on <c>Code</c> alone.
    /// Tenants can read but not create, update, or deactivate entries.
    /// </summary>
    Global = 0,

    /// <summary>
    /// Data isolated per tenant, managed by the tenant administrator.
    /// <c>TenantId</c> is always set to the current tenant. Unique constraint on
    /// <c>(Code, TenantId)</c> — different tenants can use the same code independently.
    /// </summary>
    Tenant = 1,
}
