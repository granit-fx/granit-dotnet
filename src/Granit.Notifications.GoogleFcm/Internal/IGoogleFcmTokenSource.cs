namespace Granit.Notifications.GoogleFcm.Internal;

/// <summary>
/// Mints OAuth 2.0 access tokens for the FCM HTTP v1 API. Seam over the Google
/// auth library so caching and single-flight logic can be tested without network I/O.
/// </summary>
internal interface IGoogleFcmTokenSource
{
    /// <summary>Requests a fresh access token from Google (no caching at this layer).</summary>
    Task<GoogleFcmAccessToken> MintTokenAsync(CancellationToken cancellationToken = default);
}
