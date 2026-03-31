using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// ISO 27001-compliant audit store for delivery attempts.
/// </summary>
/// <remarks>
/// <para>
/// Records are INSERT-only during the HDS retention period. After retention expires,
/// <see cref="DeleteBeforeAsync"/> enables RGPD-compliant data minimization.
/// </para>
/// </remarks>
internal sealed class EfCoreNotificationDeliveryStore(
    IDbContextFactory<NotificationsDbContext> contextFactory)
    : EfStoreBase<NotificationDeliveryAttempt, NotificationsDbContext>(contextFactory), INotificationDeliveryWriter
{
    /// <inheritdoc/>
    public Task RecordAsync(NotificationDeliveryAttempt attempt, CancellationToken cancellationToken = default) =>
        AddAsync(attempt, cancellationToken);

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
}
