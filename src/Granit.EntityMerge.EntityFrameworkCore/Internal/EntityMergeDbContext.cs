using Granit.DataFiltering;
using Granit.EntityMerge.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.EntityMerge.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext owning the small bookkeeping tables of the merge orchestrator: the
/// idempotency cache (<see cref="MergeIdempotencyEntry"/>). Aggregate-agnostic — this
/// context never references Party / Invoice / etc. directly.
/// </summary>
internal sealed class EntityMergeDbContext(
    DbContextOptions<EntityMergeDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Replay cache for Stripe-style idempotency-key support on merges.</summary>
    internal DbSet<MergeIdempotencyEntry> MergeIdempotencyEntries =>
        Set<MergeIdempotencyEntry>();

    /// <inheritdoc />
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ConfigureEntityMergeModule();
    }
}
