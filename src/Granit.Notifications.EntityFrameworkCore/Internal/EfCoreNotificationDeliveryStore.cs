using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Persistence.EntityFrameworkCore.ExceptionHandling;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// ISO 27001-compliant audit store for delivery attempts.
/// </summary>
/// <remarks>
/// <para>
/// Delivery attempts are platform-level audit rows — they always land in the host context
/// regardless of <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode"/>. This
/// keeps the SOC2 trail centralized and cross-tenant-queryable, even under Segregated
/// storage (the parent notification may live in a tenant DB, but the audit row tracking
/// the attempt stays in host for compliance ops).
/// </para>
/// <para>
/// Records carry a unique <see cref="NotificationDeliveryAttempt.DeliveryId"/> captured
/// at claim time (#947) before the outbound transport runs, so duplicate claims cannot
/// double-send.
/// </para>
/// </remarks>
internal sealed class EfCoreNotificationDeliveryStore(
    NotificationsContextResolver resolver,
    IClock clock) : INotificationDeliveryWriter
{
    // Rows claimed but never finalized (worker crash, host OOM) are eligible for
    // re-acquisition after this window. Keeps the audit row in place but unblocks
    // subsequent retries instead of permanently dropping the notification.
    internal static readonly TimeSpan InFlightClaimTimeout = TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    public async Task<bool> HasBeenDeliveredAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(tenantId: null, cancellationToken).ConfigureAwait(false);
        return await db.DeliveryAttempts
            .AnyAsync(a => a.DeliveryId == deliveryId && a.IsSuccess == true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> TryAcquireDeliveryAttemptAsync(
        NotificationDeliveryAttempt claim,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        claim.IsSuccess = null;

        try
        {
            await using INotificationsDbContext db = await resolver
                .OpenForScopeAsync(tenantId: null, cancellationToken).ConfigureAwait(false);
            db.DeliveryAttempts.Add(claim);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (DbUpdateExceptionHelper.IsDuplicateKeyException(ex))
        {
            return await ResumeFailedOrStuckDeliveryAsync(claim.DeliveryId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task CompleteDeliveryAttemptAsync(
        Guid deliveryId, bool success, long durationMilliseconds, string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(tenantId: null, cancellationToken).ConfigureAwait(false);
        await db.DeliveryAttempts
            .Where(a => a.DeliveryId == deliveryId && a.IsSuccess == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(a => a.IsSuccess, success)
                    .SetProperty(a => a.DurationMs, durationMilliseconds)
                    .SetProperty(a => a.ErrorMessage, errorMessage),
                cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> DeleteBeforeAsync(
        DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken = default)
    {
        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(tenantId: null, cancellationToken).ConfigureAwait(false);
        return await db.DeliveryAttempts
            .Where(a => a.OccurredAt < cutoff)
            .OrderBy(a => a.OccurredAt)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ResumeFailedOrStuckDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.Now;
        DateTimeOffset stuckCutoff = now - InFlightClaimTimeout;

        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(tenantId: null, cancellationToken).ConfigureAwait(false);
        int updated = await db.DeliveryAttempts
            .Where(a => a.DeliveryId == deliveryId
                     && (a.IsSuccess == false || (a.IsSuccess == null && a.OccurredAt < stuckCutoff)))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(a => a.IsSuccess, (bool?)null)
                    .SetProperty(a => a.OccurredAt, now),
                cancellationToken).ConfigureAwait(false);
        return updated == 1;
    }
}
