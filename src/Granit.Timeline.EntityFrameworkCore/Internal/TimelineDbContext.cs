using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timeline.Domain;
using Granit.Timeline.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Timeline.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core DbContext for timeline persistence.
/// </summary>
/// <remarks>
/// Isolated from the host application's DbContext to avoid coupling.
/// Compatible with PostgreSQL (ISO 27001 compliant).
/// </remarks>
internal sealed class TimelineDbContext(
    DbContextOptions<TimelineDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Activity stream entries (comments, system logs, internal notes).</summary>
    public DbSet<TimelineEntry> TimelineEntries => Set<TimelineEntry>();

    /// <summary>Attachment references linking entries to BlobStorage blobs.</summary>
    public DbSet<TimelineAttachment> TimelineAttachments => Set<TimelineAttachment>();

    /// <summary>Per-entry user reactions (👍 / ❤️ / 🎉 / 😂 / 👀) per ADR-046 + story C1.</summary>
    public DbSet<Reaction> Reactions => Set<Reaction>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureTimelineModule();
}
