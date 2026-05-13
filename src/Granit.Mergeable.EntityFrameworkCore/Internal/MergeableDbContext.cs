using Granit.DataFiltering;
using Granit.Mergeable.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Mergeable.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated DbContext owning the small bookkeeping tables of the merge orchestrator: the
/// idempotency cache (<see cref="MergeIdempotencyEntry"/>). Aggregate-agnostic — this
/// context never references Party / Invoice / etc. directly.
/// </summary>
internal sealed class MergeableDbContext(
    DbContextOptions<MergeableDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>Replay cache for Stripe-style idempotency-key support on merges.</summary>
    internal DbSet<MergeIdempotencyEntry> MergeIdempotencyEntries =>
        Set<MergeIdempotencyEntry>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureMergeableModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
