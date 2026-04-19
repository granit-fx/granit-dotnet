using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Privacy.EntityFrameworkCore.DataDeletion.Internal;

internal sealed class EfDeletionRequestTracker(
    IDbContextFactory<PrivacyDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : EfStoreBase<DeletionRequestEntity, PrivacyDbContext>(contextFactory, currentTenant),
      IDeletionRequestTrackerReader, IDeletionRequestTrackerWriter
{
    private readonly IDbContextFactory<PrivacyDbContext> _contextFactory = contextFactory;

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
        await using PrivacyDbContext db = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<DeletionRequestEntity> rows = await Query(db)
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.RequestedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(Project);
    }

    public async Task<IReadOnlyList<DeletionRequestStatus>> GetExpiredDeferredAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using PrivacyDbContext db = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<DeletionRequestEntity> rows = await Query(db)
            .Where(e => e.State == DeletionRequestState.Deferred && e.ScheduledDeletionAt <= now)
            .OrderBy(e => e.ScheduledDeletionAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(Project);
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
        }, cancellationToken);

    public Task MarkCancelledAsync(Guid requestId, DateTimeOffset cancelledAt, CancellationToken cancellationToken = default) =>
        ApplyTransitionAsync(requestId, e =>
        {
            e.State = DeletionRequestState.Cancelled;
            e.CancelledAt = cancelledAt;
        }, cancellationToken);

    private async Task ApplyTransitionAsync(
        Guid requestId,
        Action<DeletionRequestEntity> mutation,
        CancellationToken cancellationToken)
    {
        await using PrivacyDbContext db = await _contextFactory
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
            entity.TenantId?.ToString());
}
