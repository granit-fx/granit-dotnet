using Granit.Guids;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="INotificationSubscriptionReader"/> and
/// <see cref="INotificationSubscriptionWriter"/> backed by PostgreSQL.
/// </summary>
internal sealed class EfCoreNotificationSubscriptionStore(
    IDbContextFactory<NotificationsDbContext> contextFactory,
    IGuidGenerator guidGenerator)
    : EfStoreBase<NotificationSubscription, NotificationsDbContext>(contextFactory), INotificationSubscriptionReader, INotificationSubscriptionWriter
{
    /// <inheritdoc/>
    public async Task SubscribeAsync(string userId, string notificationTypeName, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        bool exists = await AnyAsync(s => s.UserId == userId && s.NotificationTypeName == notificationTypeName && s.TenantId == tenantId && s.EntityType == null, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            await AddAsync(new NotificationSubscription
            {
                Id = guidGenerator.Create(),
                UserId = userId,
                NotificationTypeName = notificationTypeName,
                TenantId = tenantId,
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task UnsubscribeAsync(string userId, string notificationTypeName, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await WriteAsync(async db =>
            await db.Subscriptions
                .Where(s => s.UserId == userId && s.NotificationTypeName == notificationTypeName && s.TenantId == tenantId && s.EntityType == null)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetSubscriberIdsAsync(string notificationTypeName, Guid? tenantId, CancellationToken cancellationToken = default) =>
        ReadAsync(async db =>
            (IReadOnlyList<string>)await db.Subscriptions
                .Where(s => s.NotificationTypeName == notificationTypeName && s.TenantId == tenantId && s.EntityType == null)
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<NotificationSubscription>> GetUserSubscriptionsAsync(string userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        ReadAsync(async db =>
            (IReadOnlyList<NotificationSubscription>)await db.Subscriptions
                .AsNoTracking()
                .Where(s => s.UserId == userId && s.TenantId == tenantId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    /// <inheritdoc/>
    public async Task FollowEntityAsync(string userId, string entityType, string entityId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        bool exists = await AnyAsync(s => s.UserId == userId && s.EntityType == entityType && s.EntityId == entityId && s.TenantId == tenantId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            await AddAsync(new NotificationSubscription
            {
                Id = guidGenerator.Create(),
                UserId = userId,
                NotificationTypeName = string.Empty,
                EntityType = entityType,
                EntityId = entityId,
                TenantId = tenantId,
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task UnfollowEntityAsync(string userId, string entityType, string entityId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await WriteAsync(async db =>
            await db.Subscriptions
                .Where(s => s.UserId == userId && s.EntityType == entityType && s.EntityId == entityId && s.TenantId == tenantId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetEntityFollowerIdsAsync(string entityType, string entityId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        ReadAsync(async db =>
            (IReadOnlyList<string>)await db.Subscriptions
                .Where(s => s.EntityType == entityType && s.EntityId == entityId && s.TenantId == tenantId)
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<NotificationSubscription>> GetEntityFollowersAsync(string entityType, string entityId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        ReadAsync(async db =>
            (IReadOnlyList<NotificationSubscription>)await db.Subscriptions
                .AsNoTracking()
                .Where(s => s.EntityType == entityType && s.EntityId == entityId && s.TenantId == tenantId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    /// <inheritdoc/>
    public Task<bool> IsFollowingEntityAsync(string userId, string entityType, string entityId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        AnyAsync(s => s.UserId == userId && s.EntityType == entityType && s.EntityId == entityId && s.TenantId == tenantId, cancellationToken);
}
