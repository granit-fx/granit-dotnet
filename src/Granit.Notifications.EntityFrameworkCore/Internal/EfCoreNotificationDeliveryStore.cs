using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.ExceptionHandling;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// ISO 27001-compliant audit store for delivery attempts.
/// </summary>
/// <remarks>
/// <para>
/// Records carry a unique <see cref="NotificationDeliveryAttempt.DeliveryId"/> captured at claim time
/// (#947) before the outbound transport runs, so duplicate claims cannot double-send.
/// </para>
/// <para>
/// After retention expires, <see cref="DeleteBeforeAsync"/> enables GDPR-compliant data minimization.
/// </para>
/// </remarks>
internal sealed class EfCoreNotificationDeliveryStore(
    IDbContextFactory<NotificationsDbContext> contextFactory,
    IClock clock)
    : EfStoreBase<NotificationDeliveryAttempt, NotificationsDbContext>(contextFactory), INotificationDeliveryWriter
{
    // Rows claimed but never finalized (worker crash, host OOM) are eligible for re-acquisition
    // after this window. Keeps the audit row in place but unblocks subsequent retries instead of
    // permanently dropping the notification.
    internal static readonly TimeSpan InFlightClaimTimeout = TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    public Task<bool> HasBeenDeliveredAsync(Guid deliveryId, CancellationToken cancellationToken = default) =>
        AnyAsync(a => a.DeliveryId == deliveryId && a.IsSuccess == true, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> TryAcquireDeliveryAttemptAsync(
        NotificationDeliveryAttempt claim,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        claim.IsSuccess = null;

        try
        {
            await AddAsync(claim, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (DbUpdateExceptionHelper.IsDuplicateKeyException(ex))
        {
            return await ResumeFailedOrStuckDeliveryAsync(claim.DeliveryId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public Task CompleteDeliveryAttemptAsync(
        Guid deliveryId,
        bool success,
        long durationMilliseconds,
        string? errorMessage,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            async db => await Query(db)
                .Where(a => a.DeliveryId == deliveryId && a.IsSuccess == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(a => a.IsSuccess, success)
                        .SetProperty(a => a.DurationMs, durationMilliseconds)
                        .SetProperty(a => a.ErrorMessage, errorMessage),
                    cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    /// <inheritdoc/>
    public Task<int> DeleteBeforeAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
            await db.DeliveryAttempts
                .Where(a => a.OccurredAt < cutoff)
                .OrderBy(a => a.OccurredAt)
                .Take(batchSize)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    // The unique index on DeliveryId means there is exactly one audit row per logical delivery,
    // so resume mutates that row in place rather than appending. ErrorMessage and DurationMs from
    // the prior failed attempt are intentionally preserved: CompleteDeliveryAttemptAsync overwrites
    // them with the outcome of the new attempt, so the row always reflects the LAST attempt — never
    // a half-zeroed state that would mislead operators investigating ISO 27001 audit trails.
    //
    // OccurredAt is rewritten to "now" because it is the freshness signal the in-flight TTL
    // depends on. Without this bump, a resume of a long-stale failed row would leave OccurredAt
    // far in the past — a concurrent worker arriving milliseconds later would see the TTL as
    // already expired and double-claim, regressing the duplicate-send guarantee from #947.
    private Task<bool> ResumeFailedOrStuckDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken) =>
        WriteAsync(
            async db =>
            {
                DateTimeOffset now = clock.Now;
                DateTimeOffset stuckCutoff = now - InFlightClaimTimeout;
                int updated = await Query(db)
                    .Where(a => a.DeliveryId == deliveryId
                        && (a.IsSuccess == false || (a.IsSuccess == null && a.OccurredAt < stuckCutoff)))
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(a => a.IsSuccess, (bool?)null)
                            .SetProperty(a => a.OccurredAt, now),
                        cancellationToken)
                    .ConfigureAwait(false);
                return updated == 1;
            },
            cancellationToken);
}
