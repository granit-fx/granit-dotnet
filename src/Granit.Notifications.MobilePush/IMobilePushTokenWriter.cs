namespace Granit.Notifications.MobilePush;

/// <summary>Writes (registers/removes) mobile push device tokens.</summary>
public interface IMobilePushTokenWriter
{
    /// <summary>Registers or updates a device token. The store computes the
    /// lookup-hash digest internally and upserts on
    /// <c>(DeviceTokenHash, TenantId)</c> — the encrypted <c>DeviceToken</c>
    /// column cannot serve equality lookups (random IV).</summary>
    Task RegisterAsync(
        string userId,
        string deviceToken,
        MobilePlatform platform,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a device token owned by the specified user.</summary>
    Task RemoveAsync(string deviceToken, string userId, Guid? tenantId, CancellationToken cancellationToken = default);
}
