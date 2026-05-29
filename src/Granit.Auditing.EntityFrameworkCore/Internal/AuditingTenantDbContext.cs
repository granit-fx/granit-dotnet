using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal;

/// <summary>
/// Tenant-isolated EF Core DbContext for the audit log module under
/// <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/>. Holds
/// per-tenant audit entries — <c>DROP SCHEMA &lt;tenant&gt; CASCADE</c> lessivage on
/// tenant offboarding is the RGPD Art. 17 primitive enabled by this layout (audit entries
/// carry PII via <c>UserId</c>, <c>UserName</c>, <c>IpAddress</c>, <c>UserAgent</c>, and
/// captured entity-change payloads).
/// </summary>
/// <remarks>
/// Cross-tenant SOC2 queries are preserved via host-admin materialisation: when the
/// caller has no ambient tenant, the services iterate every tenant via
/// <c>ITenantsAccessor</c> + <c>ICurrentTenant.Change</c>.
/// </remarks>
internal sealed class AuditingTenantDbContext(
    DbContextOptions<AuditingTenantDbContext> options,
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
