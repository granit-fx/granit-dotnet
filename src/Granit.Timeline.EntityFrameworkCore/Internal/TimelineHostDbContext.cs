using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timeline.Domain;
using Granit.Timeline.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Timeline.EntityFrameworkCore.Internal;

/// <summary>
/// Host-pinned EF Core DbContext that backs the timeline entries whose physical placement
/// is the host schema.
/// </summary>
/// <remarks>
/// <para>
/// Under <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared"/> this is
/// the single context that serves both host-admin and tenant entries; the <c>MultiTenant</c>
/// row-level filter enforces per-tenant isolation.
/// </para>
/// <para>
/// Under <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/> this
/// context holds <i>only</i> host-admin entries (<c>TenantId == null</c>) and tenant entries
/// live in the companion <see cref="TimelineTenantDbContext"/>.
/// </para>
/// </remarks>
internal sealed class TimelineHostDbContext(
    DbContextOptions<TimelineHostDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter), ITimelineDbContext
{
    /// <inheritdoc/>
    public DbSet<TimelineEntry> TimelineEntries => Set<TimelineEntry>();

    /// <inheritdoc/>
    public DbSet<TimelineAttachment> TimelineAttachments => Set<TimelineAttachment>();

    /// <inheritdoc/>
    public DbSet<Reaction> Reactions => Set<Reaction>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureTimelineModule();
}
