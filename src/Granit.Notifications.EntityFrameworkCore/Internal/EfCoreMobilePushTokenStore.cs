using Granit.Notifications.EntityFrameworkCore.Entities;
using Granit.Notifications.MobilePush;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IMobilePushTokenReader"/> and <see cref="IMobilePushTokenWriter"/>.
/// </summary>
internal sealed class EfCoreMobilePushTokenStore(
    IDbContextFactory<NotificationsDbContext> contextFactory)
    : EfStoreBase<MobilePushTokenEntity, NotificationsDbContext>(contextFactory), IMobilePushTokenReader, IMobilePushTokenWriter
{
    /// <inheritdoc />
    public Task<IReadOnlyList<MobilePushTokenInfo>> GetTokensAsync(
        string userId, Guid? tenantId, CancellationToken cancellationToken = default) =>
        ReadAsync(async db =>
            (IReadOnlyList<MobilePushTokenInfo>)await db.MobilePushTokens
                .Where(t => t.UserId == userId && t.TenantId == tenantId)
                .Select(t => t.ToTokenInfo())
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    /// <inheritdoc />
    public async Task RegisterAsync(MobilePushTokenInfo tokenInfo, CancellationToken cancellationToken = default)
    {
        await WriteAsync(async db =>
        {
            MobilePushTokenEntity? existing = await db.MobilePushTokens
                .FirstOrDefaultAsync(t => t.DeviceToken == tokenInfo.DeviceToken && t.TenantId == tokenInfo.TenantId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.UserId = tokenInfo.UserId;
                existing.Platform = tokenInfo.Platform;
            }
            else
            {
                db.MobilePushTokens.Add(new MobilePushTokenEntity
                {
                    UserId = tokenInfo.UserId,
                    DeviceToken = tokenInfo.DeviceToken,
                    Platform = tokenInfo.Platform,
                    TenantId = tokenInfo.TenantId,
                });
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string deviceToken, string userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await WriteAsync(async db =>
            await db.MobilePushTokens
                .Where(t => t.DeviceToken == deviceToken && t.UserId == userId && t.TenantId == tenantId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }
}
