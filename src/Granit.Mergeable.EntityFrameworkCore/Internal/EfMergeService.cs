using System.Text.Json;
using System.Transactions;
using Granit.Domain;
using Granit.Guids;
using Granit.Mergeable.Domain;
using Granit.Mergeable.EntityFrameworkCore.Domain;
using Granit.Mergeable.Exceptions;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

#pragma warning disable CA1812 // Resolved by DI as the open-generic registration for IMergeService<>.

namespace Granit.Mergeable.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IMergeService{TAggregate}"/>. Drives the full merge
/// pipeline: Stripe-style idempotency cache, pre-lock validation, advisory lock + row-level
/// <c>FOR UPDATE</c>, dry-run preview, scalar-field application via
/// <see cref="IMergeable{TSelf}.MergeFrom"/>, scatter-gather across all registered
/// <see cref="IReferenceRewriter{TAggregate}"/> participants, tombstone, chain-collapse, and
/// outbox-friendly persistence inside a single <see cref="TransactionScope"/> with
/// <see cref="IsolationLevel.Serializable"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Single-Postgres assumption</b>. Cross-module rewriters use their own
/// <c>DbContextFactory</c> but share the connection string with the parent aggregate's
/// <c>DbContext</c> in standard deployments. <see cref="TransactionScope"/> enrols every
/// connection opened during the scope into the same Postgres transaction (Npgsql native
/// support, no DTC). Failure of any rewriter triggers a rollback of every participant.
/// Multi-Postgres deployments require a saga-based fallback (out of scope for v1).
/// </para>
/// <para>
/// <b>Concurrency control</b>. Two layers:
/// <list type="bullet">
///   <item><c>pg_advisory_xact_lock</c> per-tenant via <c>MergeableConcurrencyLock</c>
///   serialises concurrent merges of the same tenant — second admin blocks until the first
///   completes.</item>
///   <item><c>SELECT … FOR UPDATE</c> on survivor + loser rows guards against races on
///   <see cref="IHasMergeTombstone.MergedIntoId"/> — re-validated after the lock to detect
///   stale-RowVersion situations.</item>
/// </list>
/// </para>
/// </remarks>
/// <typeparam name="TAggregate">The aggregate root being merged.</typeparam>
internal sealed class EfMergeService<TAggregate>(
    IMergeableAggregateAdapter<TAggregate> adapter,
    IEnumerable<IReferenceRewriter<TAggregate>> rewriters,
    IDbContextFactory<MergeableDbContext> idempotencyContextFactory,
    IGuidGenerator guidGenerator,
    IClock clock) : IMergeService<TAggregate>
    where TAggregate : AggregateRoot, IMergeable<TAggregate>
{
    /// <inheritdoc />
    public async Task<MergeResult<TAggregate>> MergePreviewAsync(
        Guid survivorId,
        Guid loserId,
        CancellationToken cancellationToken)
    {
        return await MergeAsync(
            new MergeRequest(survivorId, loserId, MergeFieldChoices.Empty, DryRun: true),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MergeResult<TAggregate>> MergeAsync(
        MergeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Idempotency cache lookup (live merges only — dry-run never caches).
        if (!request.DryRun && !string.IsNullOrEmpty(request.IdempotencyKey))
        {
            MergeResult<TAggregate>? cached = await TryReadIdempotencyCacheAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (cached is not null)
            {
                return cached;
            }
        }

        // 2. Open a Serializable transaction wrapping all participating DbContexts.
        // Async-aware: TransactionScopeAsyncFlowOption.Enabled ensures the scope follows
        // ConfigureAwait jumps. Single-Postgres deployments enrol every connection
        // opened inside via Npgsql; no DTC.
        using var scope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.Serializable, Timeout = TransactionManager.MaximumTimeout },
            TransactionScopeAsyncFlowOption.Enabled);

        // 3. Acquire idempotency DbContext for the duration of the orchestration; the same
        //    context will write to merge_idempotency at the end if the merge is live.
        await using MergeableDbContext idempotencyDb =
            await idempotencyContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await MergeableConcurrencyLock.AcquireAsync(idempotencyDb, tenantId: null, cancellationToken)
            .ConfigureAwait(false);

        // 4. Load survivor + loser via the per-aggregate adapter (bypasses the tombstone
        //    filter so the loser row stays observable).
        TAggregate? survivor = await adapter.LoadAsync(request.SurvivorId, cancellationToken)
            .ConfigureAwait(false);
        TAggregate? loser = await adapter.LoadAsync(request.LoserId, cancellationToken)
            .ConfigureAwait(false);

        ValidatePair(survivor, loser, request);

        // After the null/identity guards above, both are non-null.
        TAggregate survivorAggregate = survivor!;
        TAggregate loserAggregate = loser!;

        IReadOnlyList<FieldConflict> conflicts = survivorAggregate.GetConflicts(loserAggregate);

        // 5. Compute rewrite counts. In dry-run mode use CountAsync; in live mode the
        //    counts come from RewriteAsync results.
        var rewriteCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        IReferenceRewriter<TAggregate>[] orderedRewriters =
            rewriters.OrderBy(r => r.Description, StringComparer.Ordinal).ToArray();

        if (request.DryRun)
        {
            foreach (IReferenceRewriter<TAggregate> rewriter in orderedRewriters)
            {
                int count = await rewriter.CountAsync(request.SurvivorId, request.LoserId, cancellationToken)
                    .ConfigureAwait(false);
                rewriteCounts[rewriter.Description] = count;
            }

            // Dry-run never commits — TransactionScope disposes without Complete().
            return new MergeResult<TAggregate>(
                Merged: null,
                Conflicts: conflicts,
                RewriteCounts: rewriteCounts,
                DryRun: true);
        }

        // 6. Live merge. Apply scalar fields via the aggregate's domain method.
        survivorAggregate.MergeFrom(loserAggregate, request.Choices);

        // 7. Scatter-gather: each rewriter UPDATEs its own table, all enrolled in the
        //    ambient TransactionScope. Failure here triggers rollback of everything,
        //    including the idempotency cache write below.
        foreach (IReferenceRewriter<TAggregate> rewriter in orderedRewriters)
        {
            int count = await rewriter.RewriteAsync(request.SurvivorId, request.LoserId, cancellationToken)
                .ConfigureAwait(false);
            rewriteCounts[rewriter.Description] = count;
        }

        // 8. Tombstone the loser (sets MergedIntoId + MergedAt) and chain-collapse any
        //    earlier loser pointing at the loser we're now merging out.
        DateTimeOffset now = clock.Now;
        adapter.ApplyTombstone(loserAggregate, request.SurvivorId, now);
        await adapter.CollapseChainTombstonesAsync(request.SurvivorId, request.LoserId, cancellationToken)
            .ConfigureAwait(false);

        // 9. Persist both aggregates via the adapter (one round-trip in EF terms; the
        //    adapter is responsible for the SaveChanges call inside the transaction).
        await adapter.PersistMergedPairAsync(survivorAggregate, loserAggregate, cancellationToken)
            .ConfigureAwait(false);

        // 10. Build the result and write the idempotency cache entry (best-effort — failure
        //     to write the cache is not fatal; the next replay will simply re-execute).
        var result = new MergeResult<TAggregate>(
            Merged: survivorAggregate,
            Conflicts: conflicts,
            RewriteCounts: rewriteCounts,
            DryRun: false);

        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            await WriteIdempotencyCacheAsync(idempotencyDb, request, result, now, guidGenerator, cancellationToken)
                .ConfigureAwait(false);
        }

        scope.Complete();
        return result;
    }

    private static void ValidatePair(TAggregate? survivor, TAggregate? loser, MergeRequest request)
    {
        if (survivor is null)
        {
            throw new MergeException($"Survivor aggregate '{request.SurvivorId}' was not found.");
        }
        if (loser is null)
        {
            throw new MergeException($"Loser aggregate '{request.LoserId}' was not found.");
        }
        if (request.SurvivorId == request.LoserId)
        {
            throw new MergeException("Survivor and loser ids must differ.");
        }
        if (survivor.MergedIntoId is not null)
        {
            throw new MergeException(
                $"Survivor '{request.SurvivorId}' is itself merged into '{survivor.MergedIntoId}' — pick the current survivor.");
        }
        if (loser.MergedIntoId is not null)
        {
            throw new MergeException(
                $"Loser '{request.LoserId}' has already been merged (into '{loser.MergedIntoId}').");
        }
    }

    private async Task<MergeResult<TAggregate>?> TryReadIdempotencyCacheAsync(
        MergeRequest request,
        CancellationToken cancellationToken)
    {
        string hash = MergeRequestHasher.ComputeHash(request);

        await using MergeableDbContext db =
            await idempotencyContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Same key + same hash → replay
        MergeIdempotencyEntry? matchByHash = await db.MergeIdempotencyEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.Key == request.IdempotencyKey && e.RequestHash == hash,
                cancellationToken)
            .ConfigureAwait(false);

        if (matchByHash is not null)
        {
            // The cached MergeResult.Merged field cannot be reconstructed from the cache (it
            // would require deserialising the full aggregate); rely on the hash equality
            // guarantee that a fresh load + re-run of the read-side would produce the same
            // shape. For the v1 idempotency contract we return the cached counts/conflicts
            // and a null Merged pointer — callers that need the survivor reload from
            // GetByIdAsync after the merge.
            CachedMergeResult cached = JsonSerializer.Deserialize<CachedMergeResult>(matchByHash.ResultJson)!;
            return new MergeResult<TAggregate>(
                Merged: null,
                Conflicts: cached.Conflicts,
                RewriteCounts: cached.RewriteCounts,
                DryRun: false);
        }

        // Same key + different hash → 409 (key reused for a different intent)
        bool keyClash = await db.MergeIdempotencyEntries
            .AsNoTracking()
            .AnyAsync(e => e.Key == request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (keyClash)
        {
            throw new MergeException(
                $"Idempotency key '{request.IdempotencyKey}' was reused with a different request body. " +
                "Use a fresh key for a different merge intent.");
        }

        return null;
    }

    private static async Task WriteIdempotencyCacheAsync(
        MergeableDbContext db,
        MergeRequest request,
        MergeResult<TAggregate> result,
        DateTimeOffset now,
        IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        var cached = new CachedMergeResult(result.Conflicts, result.RewriteCounts);
        var entry = new MergeIdempotencyEntry
        {
            Id = guidGenerator.Create(),
            Key = request.IdempotencyKey!,
            RequestHash = MergeRequestHasher.ComputeHash(request),
            SurvivorId = request.SurvivorId,
            LoserId = request.LoserId,
            ResultJson = JsonSerializer.Serialize(cached),
            CreatedAt = now,
        };
        db.MergeIdempotencyEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record CachedMergeResult(
        IReadOnlyList<FieldConflict> Conflicts,
        IReadOnlyDictionary<string, int> RewriteCounts);
}
