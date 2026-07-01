namespace Granit.Notifications.MobilePush.Endpoints.Dtos;

/// <summary>Request to register a mobile push device token.</summary>
public sealed record MobilePushTokenRegisterRequest
{
    /// <summary>Device token from FCM/APNs.</summary>
    public required string DeviceToken { get; init; }

    /// <summary>Device platform.</summary>
    public required MobilePlatform Platform { get; init; }
}
