using Granit.MultiTenancy;
using Granit.Notifications.WebPush.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.WebPush.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IWebPushSubscriptionReader"/> and
/// <see cref="IWebPushSubscriptionWriter"/>, backed by an isolated
/// <see cref="WebPushDbContext"/>. Upsert / remove key on the unique
/// <see cref="WebPushSubscription.Endpoint"/>; key material is decrypted on read
/// via the encryption interceptor.
/// </summary>
internal sealed class EfCoreWebPushSubscriptionStore(
    IDbContextFactory<WebPushDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : EfStoreBase<WebPushSubscription, WebPushDbContext>(contextFactory, currentTenant),
      IWebPushSubscriptionReader, IWebPushSubscriptionWriter
{
    /// <inheritdoc />
    public Task<IReadOnlyList<WebPushSubscriptionInfo>> GetSubscriptionsAsync(
        string userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        ReadAsync(async db =>
        {
            List<WebPushSubscription> rows = await db.WebPushSubscriptions
                .Where(s => s.UserId == userId && s.TenantId == tenantId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return (IReadOnlyList<WebPushSubscriptionInfo>)[.. rows.Select(ToInfo)];
        },
        cancellationToken);

    /// <inheritdoc />
    public Task SaveSubscriptionAsync(
        string userId, WebPushSubscriptionInfo subscription, Guid? tenantId, CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            WebPushSubscription? existing = await db.WebPushSubscriptions
                .FirstOrDefaultAsync(s => s.Endpoint == subscription.Endpoint && s.TenantId == tenantId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.Update(userId, subscription.P256dh, subscription.Auth, subscription.ExpirationTime);
            }
            else
            {
                db.WebPushSubscriptions.Add(WebPushSubscription.Create(
                    userId, subscription.Endpoint, subscription.P256dh, subscription.Auth, subscription.ExpirationTime, tenantId));
            }
        },
        cancellationToken);

    /// <inheritdoc />
    public Task RemoveSubscriptionAsync(string endpoint, Guid? tenantId, CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
            await db.WebPushSubscriptions
                .Where(s => s.Endpoint == endpoint && s.TenantId == tenantId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    private static WebPushSubscriptionInfo ToInfo(WebPushSubscription s) => new()
    {
        Endpoint = s.Endpoint,
        ExpirationTime = s.ExpirationTime,
        P256dh = s.P256dh,
        Auth = s.Auth,
    };
}
