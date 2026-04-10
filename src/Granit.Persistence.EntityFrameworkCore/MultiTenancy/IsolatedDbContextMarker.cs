namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Marker registered by <c>AddGranitIsolatedDbContext&lt;T&gt;</c> to identify DbContext types
/// that require tenant isolation. Used by the migration runner to skip these contexts
/// when no tenants exist (cold start) — they should only be migrated per-tenant.
/// </summary>
public sealed class IsolatedDbContextMarker(Type dbContextType)
{
    public Type DbContextType { get; } = dbContextType;
}
