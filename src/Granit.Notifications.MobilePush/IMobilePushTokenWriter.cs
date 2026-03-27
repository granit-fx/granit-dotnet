namespace Granit.Notifications.MobilePush;

/// <summary>Writes (registers/removes) mobile push device tokens.</summary>
public interface IMobilePushTokenWriter
{
    /// <summary>Registers or updates a device token for a user.</summary>
    Task RegisterAsync(MobilePushTokenInfo tokenInfo, CancellationToken cancellationToken = default);

    /// <summary>Removes a device token owned by the specified user.</summary>
    Task RemoveAsync(string deviceToken, string userId, Guid? tenantId, CancellationToken cancellationToken = default);
}
