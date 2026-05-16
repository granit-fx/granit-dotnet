namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Marker registered by <c>AddGranitIsolatedDbContext&lt;T&gt;</c> to identify DbContext types
/// that require tenant isolation. Used by the migration runner to skip these contexts
/// when no tenants exist (cold start) — they should only be migrated per-tenant.
/// </summary>
/// <remarks>
/// Also carries the set of <see cref="TenantIsolationStrategy"/> values for which a keyed
/// <see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{TContext}"/> factory was registered.
/// Consumed by <see cref="TenantIsolationFactoryRegistrationValidator"/> to fail-fast at
/// startup when the active strategy has no matching factory.
/// </remarks>
public sealed class IsolatedDbContextMarker
{
    /// <summary>
    /// Initializes a new <see cref="IsolatedDbContextMarker"/>.
    /// </summary>
    /// <param name="dbContextType">The isolated <see cref="Microsoft.EntityFrameworkCore.DbContext"/> type.</param>
    /// <param name="registeredStrategies">
    /// The set of isolation strategies for which a keyed factory was registered.
    /// </param>
    public IsolatedDbContextMarker(
        Type dbContextType,
        IReadOnlySet<TenantIsolationStrategy> registeredStrategies)
    {
        DbContextType = dbContextType;
        RegisteredStrategies = registeredStrategies;
    }

    /// <summary>The isolated <see cref="Microsoft.EntityFrameworkCore.DbContext"/> type.</summary>
    public Type DbContextType { get; }

    /// <summary>
    /// Set of <see cref="TenantIsolationStrategy"/> values for which a keyed factory was
    /// registered in DI for <see cref="DbContextType"/>.
    /// </summary>
    public IReadOnlySet<TenantIsolationStrategy> RegisteredStrategies { get; }
}
