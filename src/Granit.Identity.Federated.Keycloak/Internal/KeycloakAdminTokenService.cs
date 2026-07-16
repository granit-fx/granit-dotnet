using Granit.Identity.Federated.Internal;
using Granit.Identity.Federated.Keycloak.Diagnostics;
using Granit.Identity.Federated.Keycloak.Options;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// Singleton service that obtains and caches a Keycloak service-account token using the OAuth 2.0
/// <c>client_credentials</c> flow. Thin adapter over the shared
/// <see cref="OAuth2ClientCredentialsTokenService"/>: it supplies the realm token endpoint,
/// credentials (no scope), and the Keycloak span.
/// </summary>
internal sealed class KeycloakAdminTokenService(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakAdminOptions> options,
    IClock clock,
    ILogger<KeycloakAdminTokenService> logger) : IDisposable
{
    private readonly OAuth2ClientCredentialsTokenService _tokenService = new(httpClientFactory, clock, logger);

    /// <summary>
    /// Returns a valid access token, refreshing it if expired or not yet obtained.
    /// </summary>
    public Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        KeycloakAdminOptions opts = options.Value;
        return _tokenService.GetTokenAsync(
            new OAuth2ClientCredentialsRequest(
                ProviderName: "Keycloak",
                HttpClientName: "KeycloakAdmin",
                TokenEndpoint: opts.GetTokenEndpoint(),
                ClientId: opts.ClientId,
                ClientSecret: opts.ClientSecret,
                Scope: null,
                ActivitySource: IdentityKeycloakActivitySource.Source,
                ActivityName: IdentityKeycloakActivitySource.TokenAcquire),
            cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose() => _tokenService.Dispose();
}
