using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timeline.Domain;
using Granit.Timeline.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Timeline.EntityFrameworkCore.Internal;

/// <summary>
/// Tenant-isolated EF Core DbContext used under
/// <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/>. Holds
/// tenant timeline entries (<c>TenantId == &lt;tenant&gt;</c>). Each tenant's timeline
/// table lives in its own schema (under <c>SchemaPerTenant</c>) or database (under
/// <c>DatabasePerTenant</c>) — <c>DROP SCHEMA &lt;tenant&gt; CASCADE</c> lessivage is the
/// GDPR Art. 17 right-of-erasure primitive enabled by this layout.
/// </summary>
/// <remarks>
/// Registered via <c>AddGranitIsolatedDbContext&lt;TimelineTenantDbContext&gt;</c>. The
/// companion host-side context is <see cref="TimelineHostDbContext"/>.
/// </remarks>
internal sealed class TimelineTenantDbContext(
    DbContextOptions<TimelineTenantDbContext> options,
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
