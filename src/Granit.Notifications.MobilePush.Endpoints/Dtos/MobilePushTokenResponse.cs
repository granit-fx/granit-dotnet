namespace Granit.Notifications.MobilePush.Endpoints.Dtos;

/// <summary>Response for a registered mobile push device token.</summary>
/// <param name="DeviceTokenPreview">Masked preview of the device token (last 4 characters only).
/// The plaintext token is a sendable push credential and is never returned.</param>
/// <param name="Platform">Device platform (iOS, Android).</param>
/// <param name="CreatedAt">When the token was first registered.</param>
public sealed record MobilePushTokenResponse(string DeviceTokenPreview, MobilePlatform Platform, DateTimeOffset CreatedAt);
