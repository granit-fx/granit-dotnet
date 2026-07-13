using Google.Apis.Auth.OAuth2;
using Granit.Notifications.GoogleFcm.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.GoogleFcm.Internal;

/// <summary>
/// <see cref="IGoogleFcmTokenSource"/> backed by Google.Apis.Auth: exchanges the
/// service-account key for an OAuth 2.0 access token scoped to FCM.
/// </summary>
/// <remarks>
/// The credential is rebuilt from <see cref="GoogleFcmOptions.ServiceAccountJson"/> on every
/// mint — mints happen roughly once per token lifetime (about an hour), and rebuilding keeps
/// the source coherent with configuration hot-reload.
/// </remarks>
internal sealed class GoogleApisAuthTokenSource(
    IOptionsMonitor<GoogleFcmOptions> options,
    IClock clock) : IGoogleFcmTokenSource
{
    private const string FirebaseMessagingScope = "https://www.googleapis.com/auth/firebase.messaging";

    /// <summary>Fallback lifetime when Google omits expires_in (per RFC 6749 it is optional).</summary>
    private static readonly TimeSpan DefaultTokenLifetime = TimeSpan.FromHours(1);

    /// <inheritdoc />
    public async Task<GoogleFcmAccessToken> MintTokenAsync(CancellationToken cancellationToken = default)
    {
        // CredentialFactory pins the credential type: JSON that is not a service-account
        // key is rejected at parse time instead of silently yielding another credential kind.
        GoogleCredential credential = CredentialFactory
            .FromJson(options.CurrentValue.ServiceAccountJson, JsonCredentialParameters.ServiceAccountCredentialType)
            .CreateScoped(FirebaseMessagingScope);

        if (credential.UnderlyingCredential is not ServiceAccountCredential serviceAccount)
        {
            throw new InvalidOperationException(
                "GoogleFcmOptions.ServiceAccountJson is not a service-account key "
                + $"(parsed credential type: {credential.UnderlyingCredential.GetType().Name}).");
        }

        if (!await serviceAccount.RequestAccessTokenAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Google OAuth 2.0 token endpoint refused the FCM service-account token request.");
        }

        TimeSpan lifetime = serviceAccount.Token.ExpiresInSeconds is { } seconds
            ? TimeSpan.FromSeconds(seconds)
            : DefaultTokenLifetime;

        return new GoogleFcmAccessToken(serviceAccount.Token.AccessToken, clock.Now + lifetime);
    }
}
