namespace Granit.Notifications.GoogleFcm.Internal;

/// <summary>An OAuth 2.0 access token for the FCM HTTP v1 API with its absolute expiry.</summary>
internal sealed record GoogleFcmAccessToken(string AccessToken, DateTimeOffset ExpiresAt);
