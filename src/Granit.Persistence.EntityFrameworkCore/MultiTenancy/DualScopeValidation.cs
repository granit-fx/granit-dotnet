using Granit.Persistence.MultiTenancy;

namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Validates a dual-scope module's chosen <see cref="DualScopeStorageMode"/> against the
/// host's active <see cref="TenantIsolationStrategy"/>.
/// </summary>
/// <remarks>
/// Per ADR-063, <see cref="DualScopeStorageMode.Segregated"/> combined with
/// <see cref="TenantIsolationStrategy.SharedDatabase"/> is rejected: the segregated mode
/// exists to provide physical isolation between host and tenant rows, which a shared-database
/// strategy fundamentally cannot deliver. Callers — typically a module's startup-time
/// <c>IValidateOptions&lt;T&gt;</c> implementation or a host configuration check — invoke
/// <see cref="ValidateStorageMode"/> to surface the misconfiguration before any request is served.
/// </remarks>
public static class DualScopeValidation
{
    /// <summary>
    /// Throws when the combination of <paramref name="storageMode"/> and
    /// <paramref name="strategy"/> is rejected by ADR-063.
    /// </summary>
    /// <param name="storageMode">The dual-scope storage mode declared by the module's options.</param>
    /// <param name="strategy">The host-wide tenant isolation strategy in effect.</param>
    /// <param name="moduleName">
    /// Name of the calling module (e.g. <c>"Webhooks"</c>), used in the error message to
    /// help the operator locate the misconfigured registration.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="storageMode"/> is <see cref="DualScopeStorageMode.Segregated"/>
    /// and <paramref name="strategy"/> is <see cref="TenantIsolationStrategy.SharedDatabase"/>.
    /// </exception>
    public static void ValidateStorageMode(
        DualScopeStorageMode storageMode,
        TenantIsolationStrategy strategy,
        string moduleName)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);

        if (storageMode == DualScopeStorageMode.Segregated && strategy == TenantIsolationStrategy.SharedDatabase)
        {
            throw new InvalidOperationException(
                $"{moduleName}: DualScopeStorageMode.Segregated is incompatible with " +
                "TenantIsolationStrategy.SharedDatabase. Segregated mode requires physical separation " +
                "between host and tenant rows, which SharedDatabase cannot provide. Either set " +
                "StorageMode to DualScopeStorageMode.Shared (single host table with row-level filter), " +
                "or change MultiTenancy:TenantIsolation:Strategy to SchemaPerTenant or DatabasePerTenant. " +
                "See ADR-063 for the storage-mode taxonomy.");
        }
    }
}
