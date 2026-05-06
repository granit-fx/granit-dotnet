using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.ExceptionHandling;
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
    IDbContextFactory<NotificationsDbContext> contextFactory)
    : EfStoreBase<NotificationDeliveryAttempt, NotificationsDbContext>(contextFactory), INotificationDeliveryWriter
{
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
            return await ResumeFailedDeliveryForRetryAsync(claim.DeliveryId, cancellationToken)
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

    private Task<bool> ResumeFailedDeliveryForRetryAsync(Guid deliveryId, CancellationToken cancellationToken) =>
        ReadAsync(
            async db =>
            {
                int updated = await Query(db)
                    .Where(a => a.DeliveryId == deliveryId && a.IsSuccess == false)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(a => a.IsSuccess, (bool?)null)
                            .SetProperty(a => a.ErrorMessage, (string?)null)
                            .SetProperty(a => a.DurationMs, 0L),
                        cancellationToken)
                    .ConfigureAwait(false);
                return updated == 1;
            },
            cancellationToken);
}
