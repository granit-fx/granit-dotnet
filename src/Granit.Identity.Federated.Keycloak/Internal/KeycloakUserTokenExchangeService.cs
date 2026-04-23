using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Granit.Events;
using Granit.Identity.Federated.Events;
using Granit.Identity.Federated.Keycloak.Exceptions;
using Granit.Identity.Federated.Keycloak.Options;
using Granit.Identity.Federated.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// Obtains a short-lived access token for a specific user via the OAuth 2.0 token exchange
/// flow (RFC 8693 — direct naked impersonation).
/// </summary>
/// <remarks>
/// <para>
/// Used by <see cref="KeycloakIdentityProvider"/> to call the Keycloak Account API on behalf
/// of a user when <see cref="KeycloakAdminOptions.UseTokenExchangeForDeviceActivity"/> is enabled.
/// </para>
/// <para>
/// Prerequisites on the Keycloak side:
/// <list type="bullet">
///   <item><description>Feature <c>admin-fine-grained-authz</c> must be enabled on the realm.</description></item>
///   <item><description>The service account must have the <c>realm-management:impersonation</c> role.</description></item>
/// </list>
/// </para>
/// <para>
/// Tokens are NOT cached because they are user-specific and short-lived. Each call performs
/// a fresh token exchange.
/// </para>
/// <para>
/// Every successful exchange:
/// <list type="bullet">
///   <item><description>Is gated by <see cref="ITokenExchangeRateLimiter"/> — production hosts must
///   register a distributed implementation; the in-process default lets all calls through.</description></item>
///   <item><description>Logs at <c>Information</c> level (operators can SIEM-route the message).</description></item>
///   <item><description>Publishes <see cref="IdentityTokenExchangedEto"/> via
///   <see cref="IDistributedEventBus"/> so the ISO 27001 audit trail can persist the operation.</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed partial class KeycloakUserTokenExchangeService(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakAdminOptions> options,
    ITokenExchangeRateLimiter rateLimiter,
    IDistributedEventBus distributedEventBus,
    TimeProvider timeProvider,
    ILogger<KeycloakUserTokenExchangeService> logger)
{
#pragma warning disable GRSEC003 // OAuth grant type URI constant, not a secret
    private const string TokenExchangeGrantType = "urn:ietf:params:oauth:grant-type:token-exchange";
#pragma warning restore GRSEC003

    private const string DefaultReason = "device-activity";

    /// <summary>
    /// Exchanges the service account credentials for a token representing the given user.
    /// </summary>
    /// <param name="userId">The subject user ID in the identity provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A short-lived access token for the target user.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the token endpoint returns an empty token.</exception>
    /// <exception cref="KeycloakTokenExchangeRateLimitedException">
    /// Thrown when the per-user rate limiter has rejected the call.
    /// </exception>
    public async Task<string> ExchangeTokenForUserAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userId);

        TokenExchangeRateLimitDecision decision = await rateLimiter
            .CheckAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        if (!decision.IsAllowed)
        {
            LogTokenExchangeRateLimited(userId, (long)decision.RetryAfter.TotalSeconds);
            throw new KeycloakTokenExchangeRateLimitedException(userId, decision.RetryAfter);
        }

        KeycloakAdminOptions opts = options.Value;
        HttpClient client = httpClientFactory.CreateClient("KeycloakAdmin");

        using FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("grant_type", TokenExchangeGrantType),
            new KeyValuePair<string, string>("client_id", opts.ClientId),
            new KeyValuePair<string, string>("client_secret", opts.ClientSecret),
            new KeyValuePair<string, string>("requested_subject", userId),
        ]);

        using HttpResponseMessage response = await client.PostAsync(
            opts.GetTokenEndpoint(), content, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        TokenExchangeResponse? token = await response.Content
            .ReadFromJsonAsync<TokenExchangeResponse>(cancellationToken).ConfigureAwait(false);

        if (token is null || string.IsNullOrEmpty(token.AccessToken))
        {
            throw new InvalidOperationException(
                $"Keycloak token exchange for user '{userId}' returned an empty access token.");
        }

        DateTimeOffset occurredAt = timeProvider.GetUtcNow();
        LogTokenExchangeSucceeded(userId, token.ExpiresIn);

        // Publish the audit event so SIEM consumers (Wolverine handlers, audit pipelines)
        // can persist an immutable trail of every privileged token-exchange operation.
        await distributedEventBus.PublishAsync(
            new IdentityTokenExchangedEto(userId, DefaultReason, occurredAt),
            cancellationToken).ConfigureAwait(false);

        return token.AccessToken;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Keycloak token exchange succeeded for user {UserId} (expires in {ExpiresIn}s)")]
    private partial void LogTokenExchangeSucceeded(string userId, int expiresIn);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak token exchange rate-limited for user {UserId}; retry after {RetryAfterSeconds}s")]
    private partial void LogTokenExchangeRateLimited(string userId, long retryAfterSeconds);

    private sealed record TokenExchangeResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
