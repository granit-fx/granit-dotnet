using Granit.Notifications.MobilePush;
using Granit.Notifications.MobilePush.Domain;
using Granit.Notifications.MobilePush.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IMobilePushTokenReader"/> and
/// <see cref="IMobilePushTokenWriter"/>. Upsert / remove paths route through
/// <see cref="IMobilePushTokenHasher"/> since
/// <see cref="MobilePushToken.DeviceToken"/> is encrypted at rest and not queryable for
/// equality.
/// </summary>
internal sealed class EfCoreMobilePushTokenStore(
    NotificationsContextResolver resolver,
    IMobilePushTokenHasher hasher)
    : IMobilePushTokenReader, IMobilePushTokenWriter
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<MobilePushToken>> GetTokensAsync(
        string userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return await db.MobilePushTokens
            .Where(t => t.UserId == userId && t.TenantId == tenantId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RegisterAsync(
        string userId, string deviceToken, MobilePlatform platform, Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        string tokenHash = hasher.ComputeHash(deviceToken)
            ?? throw new ArgumentException("DeviceToken cannot be null or empty.", nameof(deviceToken));

        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(tenantId, cancellationToken).ConfigureAwait(false);

        MobilePushToken? existing = await db.MobilePushTokens
            .FirstOrDefaultAsync(
                t => t.DeviceTokenHash == tokenHash && t.TenantId == tenantId,
                cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            existing.Reassign(userId, platform);
        }
        else
        {
            db.MobilePushTokens.Add(MobilePushToken.Create(userId, deviceToken, tokenHash, platform, tenantId));
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(
        string deviceToken, string userId, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        string? tokenHash = hasher.ComputeHash(deviceToken);
        if (tokenHash is null)
        {
            return;
        }

        await using INotificationsDbContext db = await resolver
            .OpenForScopeAsync(tenantId, cancellationToken).ConfigureAwait(false);
        await db.MobilePushTokens
            .Where(t => t.DeviceTokenHash == tokenHash && t.UserId == userId && t.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
