using Granit.AuditLog.Domain;
using Granit.AuditLog.EntityFrameworkCore.Extensions;
using Granit.Core.DataFiltering;
using Granit.Core.MultiTenancy;
using Granit.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.AuditLog.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated EF Core DbContext for the audit log module.
/// </summary>
/// <remarks>
/// <para>
/// This DbContext must <b>NOT</b> have <c>AuditLogChangeTrackingInterceptor</c>
/// registered — doing so would cause infinite recursion (interceptor captures changes,
/// writes to channel, worker persists to this DbContext, which triggers the interceptor again).
/// </para>
/// <para>
/// Compatible with SQL Server and PostgreSQL.
/// </para>
/// </remarks>
internal sealed class AuditLogDbContext(
    DbContextOptions<AuditLogDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>Root audit log entries.</summary>
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    /// <summary>Entity-level changes within audit entries.</summary>
    public DbSet<AuditEntityChange> AuditEntityChanges => Set<AuditEntityChange>();

    /// <summary>Property-level changes within entity changes.</summary>
    public DbSet<AuditPropertyChange> AuditPropertyChanges => Set<AuditPropertyChange>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureAuditLogModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
