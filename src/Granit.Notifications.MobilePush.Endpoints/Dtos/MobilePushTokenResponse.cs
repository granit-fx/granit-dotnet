namespace Granit.Notifications.MobilePush.Endpoints.Dtos;

/// <summary>Response for a mobile push device token.</summary>
public sealed record MobilePushTokenResponse(string DeviceToken, MobilePlatform Platform, DateTimeOffset CreatedAt);
