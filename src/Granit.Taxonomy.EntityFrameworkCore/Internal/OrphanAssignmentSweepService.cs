using Granit.Taxonomy.Diagnostics;
using Granit.Taxonomy.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Taxonomy.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core-backed implementation of <see cref="IOrphanAssignmentSweepService"/>.
/// </summary>
/// <remarks>
/// <para>
/// One pass over <c>TagAssignment</c> + <c>CategoryAssignment</c> collects every
/// distinct <c>(TenantId, TargetType, TargetId)</c> triplet, then asks the probe
/// keyed by <c>TargetType</c> whether the underlying aggregate still exists.
/// Triplets reporting "gone" are deleted in batches of <see cref="BatchSize"/>
/// via two <c>ExecuteDeleteAsync</c> calls — bypasses the change tracker so the
/// sweep stays cheap on backlogs of thousands of orphans.
/// </para>
/// <para>
/// Probe resolution uses keyed services: <c>AddTaggableExistenceProbe</c>
/// registers each probe under the aggregate's full type name. Unregistered
/// target types resolve to <see cref="AlwaysExistsProbe"/> — they're skipped, so
/// the sweep never deletes assignments without an oracle.
/// </para>
/// </remarks>
internal sealed partial class OrphanAssignmentSweepService(
    IDbContextFactory<TaxonomyDbContext> contextFactory,
    IServiceScopeFactory scopeFactory,
    TaxonomyMetrics metrics,
    ILogger<OrphanAssignmentSweepService> logger)
    : IOrphanAssignmentSweepService
{
    /// <summary>
    /// Page size for the candidate-triplet probing + deletion loop. Caps the
    /// transaction size when a host comes back online with a large orphan
    /// backlog.
    /// </summary>
    internal const int BatchSize = 500;

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        await using TaxonomyDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<AssignmentTriplet> tagPairs = await context.TagAssignments
            .Select(a => new AssignmentTriplet(a.TenantId, a.TargetType, a.TargetId))
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<AssignmentTriplet> categoryPairs = await context.CategoryAssignments
            .Select(a => new AssignmentTriplet(a.TenantId, a.TargetType, a.TargetId))
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<AssignmentTriplet> distinct = [.. tagPairs, .. categoryPairs];
        if (distinct.Count == 0)
        {
            return 0;
        }

        int totalDeleted = 0;
        AssignmentTriplet[] candidates = [.. distinct];

        for (int offset = 0; offset < candidates.Length; offset += BatchSize)
        {
            int chunkLength = Math.Min(BatchSize, candidates.Length - offset);
            ArraySegment<AssignmentTriplet> chunk = new(candidates, offset, chunkLength);

            // Resolve probes inside a fresh DI scope per batch — they may carry
            // scoped DbContexts that must not outlive the chunk.
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

            foreach (AssignmentTriplet triplet in chunk)
            {
                ITaggableExistenceProbe probe = scope.ServiceProvider
                    .GetKeyedService<ITaggableExistenceProbe>(triplet.TargetType)
                    ?? AlwaysExistsProbe.Instance;

                bool exists;
                try
                {
                    exists = await probe
                        .ExistsAsync(triplet.TenantId, triplet.TargetId, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A misbehaving probe must not abort the entire sweep. Log and
                    // treat the target as still-existing to avoid false-positive
                    // deletions on transient errors.
                    Log.ProbeFailed(logger, triplet.TargetType, triplet.TargetId, ex);
                    continue;
                }

                if (exists)
                {
                    continue;
                }

                int tagDeleted = await context.TagAssignments
                    .Where(a => a.TenantId == triplet.TenantId
                        && a.TargetType == triplet.TargetType
                        && a.TargetId == triplet.TargetId)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                int categoryDeleted = await context.CategoryAssignments
                    .Where(a => a.TenantId == triplet.TenantId
                        && a.TargetType == triplet.TargetType
                        && a.TargetId == triplet.TargetId)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                int deleted = tagDeleted + categoryDeleted;
                if (deleted == 0)
                {
                    continue;
                }
                totalDeleted += deleted;
                metrics.RecordOrphanCleanupDeleted(
                    triplet.TenantId?.ToString(),
                    triplet.TargetType,
                    deleted);
            }
        }

        if (totalDeleted > 0)
        {
            Log.SweepCompleted(logger, totalDeleted);
        }

        return totalDeleted;
    }

    private readonly record struct AssignmentTriplet(Guid? TenantId, string TargetType, Guid TargetId);

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Taxonomy orphan sweep removed {Count} assignment row(s).")]
        public static partial void SweepCompleted(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Warning,
            Message = "ITaggableExistenceProbe for target type '{TargetType}' (id {TargetId}) threw; treating target as still-existing.")]
        public static partial void ProbeFailed(ILogger logger, string targetType, Guid targetId, Exception exception);
    }
}
