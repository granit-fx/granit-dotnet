using Granit.Identity.Federated.EntraId.Diagnostics;
using Granit.Identity.Federated.EntraId.Options;
using Granit.Identity.Federated.Internal;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.EntraId.Internal;

/// <summary>
/// Singleton service that obtains and caches a Microsoft Entra ID service-account token using the
/// OAuth 2.0 <c>client_credentials</c> flow. Thin adapter over the shared
/// <see cref="OAuth2ClientCredentialsTokenService"/>: it supplies the Graph token endpoint,
/// credentials, the <c>https://graph.microsoft.com/.default</c> scope, and the Entra ID span.
/// </summary>
internal sealed class EntraIdAdminTokenService(
    IHttpClientFactory httpClientFactory,
    IOptions<EntraIdAdminOptions> options,
    IClock clock,
    ILogger<EntraIdAdminTokenService> logger) : IDisposable
{
    private readonly OAuth2ClientCredentialsTokenService _tokenService = new(httpClientFactory, clock, logger);

    /// <summary>
    /// Returns a valid access token, refreshing it if expired or not yet obtained.
    /// </summary>
    public Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        EntraIdAdminOptions opts = options.Value;
        return _tokenService.GetTokenAsync(
            new OAuth2ClientCredentialsRequest(
                ProviderName: "Entra ID",
                HttpClientName: "MicrosoftGraph",
                TokenEndpoint: opts.GetTokenEndpoint(),
                ClientId: opts.ClientId,
                ClientSecret: opts.ClientSecret,
                Scope: "https://graph.microsoft.com/.default",
                ActivitySource: IdentityEntraIdActivitySource.Source,
                ActivityName: IdentityEntraIdActivitySource.TokenAcquire),
            cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose() => _tokenService.Dispose();
}
