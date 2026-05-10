using Granit.Notifications.MobilePush.Domain;

namespace Granit.Notifications.MobilePush;

/// <summary>Reads mobile push device tokens.</summary>
public interface IMobilePushTokenReader
{
    /// <summary>Gets all device tokens for a user.</summary>
    Task<IReadOnlyList<MobilePushToken>> GetTokensAsync(string userId, Guid? tenantId, CancellationToken cancellationToken = default);
}
