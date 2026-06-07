namespace Granit.MultiTenancy;

/// <summary>
/// Service for accessing the current tenant and overriding its context.
/// Resolved as <see cref="NullTenantContext"/> when <c>Granit.MultiTenancy</c> is not
/// included in the module tree (single-tenant or tooling applications).
/// </summary>
public interface ICurrentTenant
{
    /// <summary>
    /// Indicates whether a tenant is active in the current context.
    /// True only if <see cref="Id"/> is non-null.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>Identifier of the current tenant, or <c>null</c> if outside a tenant context.</summary>
    Guid? Id { get; }

    /// <summary>Name of the current tenant, or <c>null</c>.</summary>
    string? Name { get; }

    /// <summary>
    /// Privacy regulation code for the current tenant (e.g., <c>"EU_GDPR"</c>), or <c>null</c>.
    /// Populated from <c>Tenant.Jurisdiction</c> when <c>ValidateTenantExistence</c> is enabled.
    /// Falls back to <c>null</c> when running without the EF Core tenant store.
    /// </summary>
    string? Jurisdiction { get; }

    /// <summary>
    /// Temporarily overrides the current tenant in the async flow.
    /// The previous tenant is restored when the scope is disposed.
    /// </summary>
    /// <param name="id">Identifier of the tenant to activate, or <c>null</c> to deactivate.</param>
    /// <param name="name">Optional tenant name.</param>
    /// <param name="jurisdiction">Optional privacy regulation code for the tenant.</param>
    /// <returns>Scope to dispose to restore the previous tenant.</returns>
    IDisposable Change(Guid? id, string? name = null, string? jurisdiction = null);
}
