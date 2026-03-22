using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Granit.Identity.Federated.Keycloak.Options;
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
/// </remarks>
internal sealed partial class KeycloakUserTokenExchangeService(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakAdminOptions> options,
    ILogger<KeycloakUserTokenExchangeService> logger)
{
#pragma warning disable GRSEC003 // OAuth grant type URI constant, not a secret
    private const string TokenExchangeGrantType = "urn:ietf:params:oauth:grant-type:token-exchange";
#pragma warning restore GRSEC003

    /// <summary>
    /// Exchanges the service account credentials for a token representing the given user.
    /// </summary>
    /// <param name="userId">The subject user ID in the identity provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A short-lived access token for the target user.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the token endpoint returns an empty token.</exception>
    public async Task<string> ExchangeTokenForUserAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userId);

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

        LogTokenExchangeSucceeded(userId, token.ExpiresIn);

        return token.AccessToken;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token exchange succeeded for user {UserId}, expires in {ExpiresIn}s")]
    private partial void LogTokenExchangeSucceeded(string userId, int expiresIn);

    private sealed record TokenExchangeResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
