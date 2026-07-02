using Granit.MultiTenancy;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Granit.Privacy.EntityFrameworkCore.DataDeletion.Internal;

/// <summary>
/// EF Core-backed implementation of the deletion request tracker, parameterised by the host
/// <typeparamref name="TContext"/>. Apps that keep privacy entities on their own shared or
/// tenant-scoped DbContext register this store against their context rather than the default
/// <c>PrivacyDbContext</c>.
/// </summary>
internal sealed class EfDeletionRequestTracker<TContext>(
    IDbContextFactory<TContext> contextFactory,
    ICurrentTenant currentTenant)
    : EfStoreBase<DeletionRequestEntity, TContext>(contextFactory, currentTenant),
      IDeletionRequestTrackerReader, IDeletionRequestTrackerWriter
    where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> _contextFactory = contextFactory;

    public async Task<DeletionRequestStatus?> GetStatusAsync(
        Guid requestId, CancellationToken cancellationToken = default)
    {
        DeletionRequestEntity? entity = await FindByIdAsync(requestId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : Project(entity);
    }

    public async Task<IReadOnlyList<DeletionRequestStatus>> GetByUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DeletionRequestEntity> rows = await ListAsync(
            Spec.For<DeletionRequestEntity>()
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.RequestedAt),
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(Project)];
    }

    public async Task<IReadOnlyList<DeletionRequestStatus>> GetExpiredDeferredAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DeletionRequestEntity> rows = await ListAsync(
            Spec.For<DeletionRequestEntity>()
                .Where(e => e.State == DeletionRequestState.Deferred && e.ScheduledDeletionAt <= now)
                .OrderBy(e => e.ScheduledDeletionAt),
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(Project)];
    }

    public Task RecordDeferredAsync(
        Guid requestId,
        Guid userId,
        string reason,
        DateTimeOffset requestedAt,
        DateTimeOffset scheduledDeletionAt,
        CancellationToken cancellationToken = default)
    {
        DeletionRequestEntity entity = new()
        {
            Id = requestId,
            UserId = userId,
            State = DeletionRequestState.Deferred,
            Reason = reason,
            RequestedAt = requestedAt,
            ScheduledDeletionAt = scheduledDeletionAt,
        };
        return AddAsync(entity, cancellationToken);
    }

    public Task MarkExecutedAsync(Guid requestId, DateTimeOffset executedAt, CancellationToken cancellationToken = default) =>
        ApplyTransitionAsync(requestId, e =>
        {
            e.State = DeletionRequestState.Executed;
            e.ExecutedAt = executedAt;
            e.MissingProviders = null;
        }, cancellationToken);

    public Task MarkCancelledAsync(Guid requestId, DateTimeOffset cancelledAt, CancellationToken cancellationToken = default) =>
        ApplyTransitionAsync(requestId, e =>
        {
            e.State = DeletionRequestState.Cancelled;
            e.CancelledAt = cancelledAt;
        }, cancellationToken);

    public Task MarkExecutingAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        ApplyTransitionAsync(requestId, e =>
        {
            // Only advance Deferred → Executing. A late acknowledgement or the enforcement-service
            // fallback may already have driven the row to Executed; never regress it.
            if (e.State == DeletionRequestState.Deferred)
            {
                e.State = DeletionRequestState.Executing;
            }
        }, cancellationToken);

    public Task MarkPartiallyExecutedAsync(
        Guid requestId,
        DateTimeOffset executedAt,
        IReadOnlyList<string> missingProviders,
        CancellationToken cancellationToken = default) =>
        ApplyTransitionAsync(requestId, e =>
        {
            e.State = DeletionRequestState.PartiallyExecuted;
            e.ExecutedAt = executedAt;
            e.MissingProviders = missingProviders.Count == 0 ? null : string.Join(',', missingProviders);
        }, cancellationToken);

    private async Task ApplyTransitionAsync(
        Guid requestId,
        Action<DeletionRequestEntity> mutation,
        CancellationToken cancellationToken)
    {
        await using TContext db = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        DeletionRequestEntity? entity = await Query(db)
            .FirstOrDefaultAsync(e => e.Id == requestId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        mutation(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DeletionRequestStatus Project(DeletionRequestEntity entity) =>
        new(
            entity.Id,
            entity.UserId,
            entity.State,
            entity.Reason,
            entity.RequestedAt,
            entity.ScheduledDeletionAt,
            entity.CancelledAt,
            entity.ExecutedAt,
            entity.Regulation,
            entity.TenantId,
            string.IsNullOrEmpty(entity.MissingProviders)
                ? null
                : entity.MissingProviders.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
