using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Granit.Identity.Federated.Keycloak.Diagnostics;
using Granit.Identity.Federated.Keycloak.Options;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// Singleton service that obtains and caches a Keycloak service account token
/// using the OAuth 2.0 <c>client_credentials</c> flow.
/// </summary>
/// <remarks>
/// Thread-safe: uses a <see cref="SemaphoreSlim"/> to serialize token refresh.
/// The token is cached until 30 seconds before its actual expiry.
/// </remarks>
internal sealed partial class KeycloakAdminTokenService(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakAdminOptions> options,
    IClock clock,
    ILogger<KeycloakAdminTokenService> logger) : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry;

    /// <summary>
    /// Returns a valid access token, refreshing it if expired or not yet obtained.
    /// </summary>
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && clock.Now < _tokenExpiry)
        {
            return _cachedToken;
        }

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring the lock.
            if (_cachedToken is not null && clock.Now < _tokenExpiry)
            {
                return _cachedToken;
            }

            using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.TokenAcquire);

            KeycloakAdminOptions opts = options.Value;
            HttpClient client = httpClientFactory.CreateClient("KeycloakAdmin");

            using FormUrlEncodedContent content = new(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", opts.ClientId),
                new KeyValuePair<string, string>("client_secret", opts.ClientSecret),
            ]);

            using HttpResponseMessage response = await client.PostAsync(
                opts.GetTokenEndpoint(), content, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            TokenResponse? token = await response.Content
                .ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false);

            if (token is null || string.IsNullOrEmpty(token.AccessToken))
            {
                throw new InvalidOperationException(
                    "Keycloak token endpoint returned an empty access token.");
            }

            // Cache with a 30-second safety margin.
            _cachedToken = token.AccessToken;
            _tokenExpiry = clock.Now.AddSeconds(Math.Max(token.ExpiresIn - 30, 10));

            LogAdminTokenObtained(_tokenExpiry);

            return _cachedToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _semaphore.Dispose();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Keycloak admin token obtained, expires at {Expiry}")]
    private partial void LogAdminTokenObtained(DateTimeOffset expiry);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
