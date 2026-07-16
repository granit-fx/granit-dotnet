using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Federated.Internal;

/// <summary>
/// Obtains and caches an OAuth 2.0 <c>client_credentials</c> service-account token, shared by the
/// federated providers whose admin APIs authenticate that way (Entra ID, Keycloak). One instance
/// per provider holds one cached token; refresh is serialized by a <see cref="SemaphoreSlim"/> with
/// a double-checked lock, and the token is cached until 30 seconds before its advertised expiry.
/// </summary>
internal sealed partial class OAuth2ClientCredentialsTokenService(
    IHttpClientFactory httpClientFactory,
    IClock clock,
    ILogger logger) : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry;

    /// <summary>
    /// Returns a valid access token for <paramref name="request"/>, refreshing it if expired or not
    /// yet obtained. Callers pass their per-provider parameters each call (options may change).
    /// </summary>
    public async Task<string> GetTokenAsync(OAuth2ClientCredentialsRequest request, CancellationToken cancellationToken)
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

            using Activity? activity = request.ActivitySource.StartActivity(request.ActivityName);

            HttpClient client = httpClientFactory.CreateClient(request.HttpClientName);

            List<KeyValuePair<string, string>> form =
            [
                new("grant_type", "client_credentials"),
                new("client_id", request.ClientId),
                new("client_secret", request.ClientSecret),
            ];

            if (!string.IsNullOrEmpty(request.Scope))
            {
                form.Add(new("scope", request.Scope));
            }

            using FormUrlEncodedContent content = new(form);

            using HttpResponseMessage response = await client.PostAsync(
                request.TokenEndpoint, content, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            TokenResponse? token = await response.Content
                .ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false);

            if (token is null || string.IsNullOrEmpty(token.AccessToken))
            {
                throw new InvalidOperationException(
                    $"{request.ProviderName} token endpoint returned an empty access token.");
            }

            // Cache with a 30-second safety margin (floored so a short-lived token still caches briefly).
            _cachedToken = token.AccessToken;
            _tokenExpiry = clock.Now.AddSeconds(Math.Max(token.ExpiresIn - 30, 10));

            LogAdminTokenObtained(logger, request.ProviderName, _tokenExpiry);

            return _cachedToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _semaphore.Dispose();

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Provider} admin token obtained, expires at {Expiry}")]
    private static partial void LogAdminTokenObtained(ILogger logger, string provider, DateTimeOffset expiry);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

/// <summary>
/// The per-provider parameters for a single <see cref="OAuth2ClientCredentialsTokenService"/> token
/// acquisition. Built from the provider's options each call.
/// </summary>
/// <param name="ProviderName">Display name for logs / error text (e.g. <c>"Entra ID"</c>).</param>
/// <param name="HttpClientName">The named <see cref="HttpClient"/> to POST the token request with.</param>
/// <param name="TokenEndpoint">The absolute token-endpoint URL.</param>
/// <param name="ClientId">The service-account client id.</param>
/// <param name="ClientSecret">The service-account client secret.</param>
/// <param name="Scope">The requested scope, or <see langword="null"/> when the provider needs none (Keycloak).</param>
/// <param name="ActivitySource">The provider's activity source for the token-acquire span.</param>
/// <param name="ActivityName">The span name for the token acquisition.</param>
internal sealed record OAuth2ClientCredentialsRequest(
    string ProviderName,
    string HttpClientName,
    string TokenEndpoint,
    string ClientId,
    string ClientSecret,
    string? Scope,
    ActivitySource ActivitySource,
    string ActivityName);
