using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated EF Core DbContext for the audit log module.
/// </summary>
/// <remarks>
/// <para>
/// This DbContext must <b>NOT</b> have <c>AuditingChangeTrackingInterceptor</c>
/// registered — doing so would cause infinite recursion (interceptor captures changes,
/// writes to channel, worker persists to this DbContext, which triggers the interceptor again).
/// </para>
/// <para>
/// Compatible with SQL Server and PostgreSQL.
/// </para>
/// </remarks>
internal sealed class AuditingDbContext(
    DbContextOptions<AuditingDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>Root audit log entries.</summary>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>Entity-level changes within audit entries.</summary>
    /// <remarks>Not dead code — EF Core requires this property for model discovery and table generation.</remarks>
    public DbSet<AuditEntityChange> AuditEntityChanges => Set<AuditEntityChange>();

    /// <summary>Property-level changes within entity changes.</summary>
    /// <remarks>Not dead code — EF Core requires this property for model discovery and table generation.</remarks>
    public DbSet<AuditPropertyChange> AuditPropertyChanges => Set<AuditPropertyChange>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureAuditingModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
