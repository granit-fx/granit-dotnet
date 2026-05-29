using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal;

/// <summary>
/// Host-pinned EF Core DbContext for the audit log module. Serves both Shared mode (single
/// context for every audit entry) and the host portion of Segregated mode (platform-level
/// audit entries — SOC2 oversight, host-admin actions, <c>TenantId == null</c>).
/// </summary>
/// <remarks>
/// <para>
/// This DbContext must <b>NOT</b> have <c>AuditingChangeTrackingInterceptor</c> registered
/// — that would cause infinite recursion (the interceptor captures changes, writes to
/// channel, worker persists to this DbContext, which would trigger the interceptor again).
/// </para>
/// </remarks>
internal sealed class AuditingHostDbContext(
    DbContextOptions<AuditingHostDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter), IAuditingDbContext
{
    /// <inheritdoc/>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <inheritdoc/>
    public DbSet<AuditEntityChange> AuditEntityChanges => Set<AuditEntityChange>();

    /// <inheritdoc/>
    public DbSet<AuditPropertyChange> AuditPropertyChanges => Set<AuditPropertyChange>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureAuditingModule();
}
