using Granit.Auditing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal;

/// <summary>
/// Common shape exposed by every Auditing DbContext flavour so services can dispatch
/// reads and writes regardless of whether the storage layout is <c>Shared</c> (one
/// context) or <c>Segregated</c> (host context + tenant context).
/// </summary>
/// <remarks>
/// Per ADR-063 the two concrete implementations are:
/// <list type="bullet">
///   <item><see cref="AuditingHostDbContext"/> — host-pinned. Serves both <c>Shared</c> mode (single context for all rows, row-level filter) and the host portion of <c>Segregated</c> mode (platform-level audit entries — SOC2 oversight, host-admin actions).</item>
///   <item><see cref="AuditingTenantDbContext"/> — tenant-isolated. Used only under <c>Segregated</c> mode for per-tenant audit entries.</item>
/// </list>
/// </remarks>
internal interface IAuditingDbContext : IAsyncDisposable, IDisposable
{
    /// <summary>Root audit log entries.</summary>
    DbSet<AuditEntry> AuditEntries { get; }

    /// <summary>Entity-level changes within audit entries.</summary>
    DbSet<AuditEntityChange> AuditEntityChanges { get; }

    /// <summary>Property-level changes within entity changes.</summary>
    DbSet<AuditPropertyChange> AuditPropertyChanges { get; }

    /// <summary>Persists pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
