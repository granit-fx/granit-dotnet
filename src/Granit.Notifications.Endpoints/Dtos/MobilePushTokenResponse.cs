using Granit.Notifications.MobilePush;

namespace Granit.Notifications.Endpoints.Dtos;

/// <summary>Response for a mobile push device token.</summary>
public sealed record MobilePushTokenResponse(string DeviceToken, MobilePlatform Platform, DateTimeOffset CreatedAt);
