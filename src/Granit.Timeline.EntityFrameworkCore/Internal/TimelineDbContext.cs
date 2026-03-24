using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.Extensions;
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
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>Activity stream entries (comments, system logs, internal notes).</summary>
    public DbSet<TimelineEntry> TimelineEntries => Set<TimelineEntry>();

    /// <summary>Attachment references linking entries to BlobStorage blobs.</summary>
    public DbSet<TimelineAttachment> TimelineAttachments => Set<TimelineAttachment>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureTimelineModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
