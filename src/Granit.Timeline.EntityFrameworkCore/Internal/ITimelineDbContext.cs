using Granit.Timeline.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Timeline.EntityFrameworkCore.Internal;

/// <summary>
/// Common shape exposed by every Timeline DbContext flavour so stores can dispatch reads
/// and writes regardless of whether the storage layout is <c>Shared</c> (one context) or
/// <c>Segregated</c> (host context + tenant context).
/// </summary>
/// <remarks>
/// Per ADR-063 the two concrete implementations are:
/// <list type="bullet">
///   <item><see cref="TimelineHostDbContext"/> — host-pinned. Serves both <c>Shared</c> mode (single context for all rows, row-level filter) and the host portion of <c>Segregated</c> mode (host-admin timeline entries only).</item>
///   <item><see cref="TimelineTenantDbContext"/> — tenant-isolated. Used only under <c>Segregated</c> mode for per-tenant entries.</item>
/// </list>
/// </remarks>
internal interface ITimelineDbContext : IAsyncDisposable, IDisposable
{
    /// <summary>Activity stream entries (comments, system logs, internal notes).</summary>
    DbSet<TimelineEntry> TimelineEntries { get; }

    /// <summary>Attachment references linking entries to BlobStorage blobs.</summary>
    DbSet<TimelineAttachment> TimelineAttachments { get; }

    /// <summary>Per-entry user reactions.</summary>
    DbSet<Reaction> Reactions { get; }

    /// <summary>Persists pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
