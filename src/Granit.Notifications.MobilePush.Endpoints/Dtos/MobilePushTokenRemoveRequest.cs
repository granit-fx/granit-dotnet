namespace Granit.Notifications.MobilePush.Endpoints.Dtos;

/// <summary>Request to unregister a mobile push device token.</summary>
/// <remarks>
/// The token travels in the request body — it is a sendable push credential, and a route
/// segment would leak it into access logs, proxy logs and browser history.
/// </remarks>
public sealed record MobilePushTokenRemoveRequest
{
    /// <summary>Device token (FCM or APNs) to remove.</summary>
    public required string DeviceToken { get; init; }
}
