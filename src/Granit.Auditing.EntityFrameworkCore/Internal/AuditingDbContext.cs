using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated EF Core DbContext for the audit log module.
/// </summary>
/// <remarks>
/// <para>
/// Used by the standalone persistence path (hosts whose audited context does not map the
/// audit entities), explicit <c>IAuditingWriter</c> writes, and the read/cleanup services.
/// The audit entities all carry <c>[AuditIgnore]</c>, so the capture interceptor sees only
/// empty batches on this context — no recursion. This context owns the audit-table DDL by
/// default; a host context that maps the entities via
/// <c>ConfigureAuditingModule(excludeFromMigrations: true)</c> shares the tables without
/// emitting duplicate migrations.
/// </para>
/// <para>
/// Compatible with SQL Server and PostgreSQL.
/// </para>
/// </remarks>
internal sealed class AuditingDbContext(
    DbContextOptions<AuditingDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
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
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureAuditingModule();
}
