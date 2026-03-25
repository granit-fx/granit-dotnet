using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
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
internal sealed class EfCoreNotificationDeliveryStore(IDbContextFactory<NotificationsDbContext> dbContextFactory) : INotificationDeliveryWriter
{
    /// <inheritdoc/>
    public async Task RecordAsync(NotificationDeliveryAttempt attempt, CancellationToken cancellationToken = default)
    {
        await using NotificationsDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.DeliveryAttempts.Add(attempt);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> DeleteBeforeAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        await using NotificationsDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.DeliveryAttempts
            .Where(a => a.OccurredAt < cutoff)
            .OrderBy(a => a.OccurredAt)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
