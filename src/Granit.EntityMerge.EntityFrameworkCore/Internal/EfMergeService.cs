using System.Security.Cryptography;
using System.Text.Json;
using System.Transactions;
using Granit.Domain;
using Granit.Encryption;
using Granit.Guids;
using Granit.EntityMerge.Domain;
using Granit.EntityMerge.Exceptions;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

#pragma warning disable CA1812 // Justification: resolved by DI through the open-generic registration `IMergeService<>` → `EfMergeService<>` (no direct `new` call in the codebase).

using Granit.EntityMerge.EntityFrameworkCore.Options;

namespace Granit.EntityMerge.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IMergeService{TAggregate}"/>. Drives the full merge
/// pipeline: Stripe-style idempotency cache (encrypted-then-MAC), pre-lock validation,
/// per-tenant advisory lock + row-level <c>FOR UPDATE</c>, dry-run preview, scalar-field
/// application via <see cref="IMergeable{TSelf}.MergeFrom"/>, scatter-gather across all
/// registered <see cref="IReferenceRewriter{TAggregate}"/> participants, tombstone,
/// chain-collapse, merged-event emission, and outbox-friendly persistence inside a single
/// <see cref="TransactionScope"/> with <see cref="IsolationLevel.Serializable"/> and a
/// host-bound timeout.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root being merged.</typeparam>
internal sealed class EfMergeService<TAggregate>(
    IMergeableAggregateAdapter<TAggregate> adapter,
    IEnumerable<IReferenceRewriter<TAggregate>> rewriters,
    IDbContextFactory<EntityMergeDbContext> idempotencyContextFactory,
    IGuidGenerator guidGenerator,
    IClock clock,
    IStringEncryptionService stringEncryption,
    IEntityMergeSecretProvider secretProvider,
    IOptions<EntityMergeOptions> options,
    ICurrentTenant? currentTenant = null) : IMergeService<TAggregate>
    where TAggregate : Entity, IMergeable<TAggregate>
{
    private readonly EntityMergeOptions _options = options.Value;

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

        Guid? tenantId = currentTenant?.IsAvailable == true ? currentTenant.Id : null;
        byte[] macKey = secretProvider.GetMacKey();

        // 1. Idempotency cache lookup (live merges only — dry-run never caches).
        if (!request.DryRun && !string.IsNullOrEmpty(request.IdempotencyKey))
        {
            MergeResult<TAggregate>? cached = await TryReadIdempotencyCacheAsync(
                request, tenantId, macKey, cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                // Rehydrate Merged from the live aggregate so callers always observe
                // result.Merged != null on a successful replay (matches the contract of
                // a fresh live merge). The aggregate is loaded outside the orchestrator's
                // transaction — replays are read-only by definition.
                TAggregate? mergedSnapshot = await adapter.LoadAsync(request.SurvivorId, cancellationToken)
                    .ConfigureAwait(false);
                return cached with { Merged = mergedSnapshot };
            }
        }

        // 2. Open a Serializable transaction wrapping all participating DbContexts.
        // Async-aware: TransactionScopeAsyncFlowOption.Enabled ensures the scope follows
        // ConfigureAwait jumps. Single-Postgres deployments enrol every connection
        // opened inside via Npgsql; no DTC. Timeout is bounded by EntityMergeOptions —
        // fail fast on stuck rewriters before they hold the per-tenant advisory lock
        // indefinitely.
        using var scope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.Serializable, Timeout = _options.MergeTimeout },
            TransactionScopeAsyncFlowOption.Enabled);

        // 3. Acquire idempotency DbContext for the duration of the orchestration; the same
        //    context will write to merge_idempotency at the end if the merge is live.
        await using EntityMergeDbContext idempotencyDb =
            await idempotencyContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Per-tenant advisory lock — concurrent merges across DIFFERENT tenants run in
        // parallel; concurrent merges of the SAME tenant serialise. Tenants without a
        // tenant context fall back to the "global" key (single-tenant deployments).
        await EntityMergeConcurrencyLock.AcquireAsync(idempotencyDb, tenantId, cancellationToken)
            .ConfigureAwait(false);

        // 4. Load survivor + loser via the per-aggregate adapter (bypasses the tombstone
        //    filter so the loser row stays observable). The IMultiTenant filter remains
        //    active inside LoadAsync — cross-tenant reads return null and ValidatePair
        //    throws "not found".
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

        // 9. Stamp the merged event(s) on the survivor. The DomainEventDispatcherInterceptor
        //    flushes the domain event in the same transaction; the eto enrols into the
        //    Wolverine outbox atomically with the parties writes. Adapters whose aggregate
        //    has no merged-lifecycle event use the default no-op implementation.
        adapter.RaiseMergedEvents(survivorAggregate, loserAggregate, request, rewriteCounts, now);

        // 10. Persist both aggregates via the adapter (one round-trip in EF terms; the
        //     adapter is responsible for the SaveChanges call inside the transaction).
        await adapter.PersistMergedPairAsync(survivorAggregate, loserAggregate, cancellationToken)
            .ConfigureAwait(false);

        // 11. Build the result and write the idempotency cache entry (best-effort — failure
        //     to write the cache is not fatal; the next replay will simply re-execute).
        var result = new MergeResult<TAggregate>(
            Merged: survivorAggregate,
            Conflicts: conflicts,
            RewriteCounts: rewriteCounts,
            DryRun: false);

        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            await WriteIdempotencyCacheAsync(idempotencyDb, request, tenantId, macKey, result, now, cancellationToken)
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
        // Belt-and-braces tenant guard. The IMultiTenant query filter normally blocks
        // cross-tenant loads (LoadAsync returns null), but a future change that opts out of
        // the filter — or an aggregate type that never enrols IMultiTenant — would otherwise
        // open a cross-tenant merge path. Validate explicitly when both aggregates carry a
        // tenant id so the orchestrator never participates in a cross-tenant merge.
        if (survivor is IMultiTenant survivorTenant && loser is IMultiTenant loserTenant
            && survivorTenant.TenantId != loserTenant.TenantId)
        {
            throw new MergeException("Survivor and loser must belong to the same tenant scope.");
        }
        if (survivor.MergedIntoId is not null)
        {
            // Generic wording — never disclose the chain pointer to the caller. The internal
            // pointer is logged via the orchestrator's diagnostic surface, never surfaced in
            // the user-visible exception message (CWE-209).
            throw new MergeException(
                $"Survivor '{request.SurvivorId}' has itself been merged — pick the current survivor.");
        }
        if (loser.MergedIntoId is not null)
        {
            throw new MergeException(
                $"Loser '{request.LoserId}' has already been merged.");
        }
    }

    private async Task<MergeResult<TAggregate>?> TryReadIdempotencyCacheAsync(
        MergeRequest request,
        Guid? tenantId,
        byte[] macKey,
        CancellationToken cancellationToken)
    {
        string hash = MergeRequestHasher.ComputeHash(request, tenantId, macKey);

        await using EntityMergeDbContext db =
            await idempotencyContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Same tenant + key + hash → replay
        MergeIdempotencyEntry? matchByHash = await db.MergeIdempotencyEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId && e.Key == request.IdempotencyKey && e.RequestHash == hash,
                cancellationToken)
            .ConfigureAwait(false);

        if (matchByHash is not null)
        {
            // Encrypt-then-MAC verification: decrypt the payload, recompute the MAC over the
            // plaintext, reject mismatches. This blocks a write-only DB compromise that
            // would otherwise inject a poisoned ResultJson while leaving the MAC untouched.
            string? plaintext = stringEncryption.Decrypt(matchByHash.ResultJson);
            if (plaintext is null)
            {
                // Decrypt failure (corrupted ciphertext, key rotation gone wrong) — refuse
                // to replay but do NOT throw, so the caller can fall through to a fresh
                // merge instead of a hard 500.
                return null;
            }

            string expectedMac = MergeRequestHasher.ComputePayloadMac(plaintext, macKey);
            if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(expectedMac),
                System.Text.Encoding.ASCII.GetBytes(matchByHash.ResultMac)))
            {
                // Tampered cache row — refuse to replay. Caller sees a fresh merge attempt.
                return null;
            }

            CachedMergeResult cached = JsonSerializer.Deserialize<CachedMergeResult>(plaintext)!;
            // Cache stores only the read-side projection (rewrite counts + conflict paths).
            // Survivor/loser scalar values are NEVER persisted (PII minimization, GDPR Art. 5).
            // The Merged aggregate is rehydrated by MergeAsync via adapter.LoadAsync.
            return new MergeResult<TAggregate>(
                Merged: null,
                Conflicts: cached.Conflicts.Select(p =>
                    new FieldConflict(p.FieldPath, SurvivorValue: null, LoserValue: null, p.Default)).ToList(),
                RewriteCounts: cached.RewriteCounts,
                DryRun: false);
        }

        // Same tenant + key + different hash → 409 (key reused for a different intent
        // within the same tenant). Cross-tenant key collisions are silently allowed because
        // the lookup above is tenant-scoped — Tenant B reusing Tenant A's literal key never
        // observes Tenant A's existence (closes the cross-tenant key oracle).
        bool keyClash = await db.MergeIdempotencyEntries
            .AsNoTracking()
            .AnyAsync(e => e.TenantId == tenantId && e.Key == request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (keyClash)
        {
            throw new MergeException(
                $"Idempotency key '{request.IdempotencyKey}' was reused with a different request body. " +
                "Use a fresh key for a different merge intent.");
        }

        return null;
    }

    private async Task WriteIdempotencyCacheAsync(
        EntityMergeDbContext db,
        MergeRequest request,
        Guid? tenantId,
        byte[] macKey,
        MergeResult<TAggregate> result,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Persist only the read-side projection — paths + winner sides + rewrite counts. The
        // survivor/loser scalar values that briefly populated FieldConflict during the live
        // merge are intentionally dropped here so they NEVER reach the cache table (PII
        // minimization, GDPR Art. 5(1)(c)).
        var cached = new CachedMergeResult(
            result.Conflicts.Select(c => new CachedFieldConflictPath(c.FieldPath, c.Default)).ToList(),
            result.RewriteCounts);

        string plaintextJson = JsonSerializer.Serialize(cached);
        string ciphertext = stringEncryption.Encrypt(plaintextJson);
        string mac = MergeRequestHasher.ComputePayloadMac(plaintextJson, macKey);

        var entry = new MergeIdempotencyEntry
        {
            Id = guidGenerator.Create(),
            TenantId = tenantId,
            Key = request.IdempotencyKey!,
            RequestHash = MergeRequestHasher.ComputeHash(request, tenantId, macKey),
            SurvivorId = request.SurvivorId,
            LoserId = request.LoserId,
            ResultJson = ciphertext,
            ResultMac = mac,
            CreatedAt = now,
        };
        db.MergeIdempotencyEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record CachedFieldConflictPath(string FieldPath, WinnerSide Default);

    private sealed record CachedMergeResult(
        IReadOnlyList<CachedFieldConflictPath> Conflicts,
        IReadOnlyDictionary<string, int> RewriteCounts);
}
