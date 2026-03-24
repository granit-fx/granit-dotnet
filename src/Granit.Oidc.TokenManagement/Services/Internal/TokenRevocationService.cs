using System.Diagnostics;
using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.Discovery;
using Granit.Oidc.DPoP;
using Granit.Oidc.Internal;
using Granit.Oidc.Requests;
using Granit.Oidc.TokenManagement.Diagnostics;
using Microsoft.Extensions.Logging;

#pragma warning disable GRSEC003 // Service handles OAuth 2.0 token revocation including access and refresh tokens

namespace Granit.Oidc.TokenManagement.Services.Internal;

/// <summary>
/// Sends revocation requests to an OAuth 2.0 revocation endpoint (RFC 7009).
/// Revocation is best-effort: failures are logged but do not throw.
/// </summary>
internal sealed partial class TokenRevocationService(
    IDiscoveryDocumentService discoveryDocumentService,
    IHttpClientFactory httpClientFactory,
    IDPoPProofService dpopProofService,
    TokenManagementMetrics metrics,
    ILogger<TokenRevocationService> logger) : ITokenRevocationService
{
    private const string HttpClientName = "Granit.TokenManagement";
    private const string DPoPHeader = "DPoP";

    /// <inheritdoc/>
    public async Task<bool> RevokeTokenAsync(
        string authority,
        RevocationRequest request,
        IClientAuthenticationStrategy? clientAuth = null,
        DPoPOptions? dpop = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(authority);
        ArgumentNullException.ThrowIfNull(request);

        using Activity? activity = TokenManagementActivitySource.Source.StartActivity(
            TokenManagementActivitySource.RevokeToken);
        activity?.SetTag("authority", authority);
        activity?.SetTag("client_id", request.ClientId);

        OidcDiscoveryDocument disco = await discoveryDocumentService
            .GetAsync(authority, cancellationToken)
            .ConfigureAwait(false);

        if (disco.RevocationEndpoint is null)
        {
            LogRevocationEndpointNotAvailable(authority);
            return false;
        }

        string revocationEndpoint = disco.RevocationEndpoint;

        Dictionary<string, string> parameters = TokenRequestEncoder.ToParameters(request);
        clientAuth?.Apply(parameters, request.ClientId, revocationEndpoint);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, revocationEndpoint)
        {
            Content = new FormUrlEncodedContent(parameters),
        };

        if (dpop is not null)
        {
            string proof = dpopProofService.CreateProof(
                dpop.PrivateKeyJwk, "POST", revocationEndpoint, dpop.Nonce);
            httpRequest.Headers.TryAddWithoutValidation(DPoPHeader, proof);
        }

        HttpClient client = httpClientFactory.CreateClient(HttpClientName);

        try
        {
            HttpResponseMessage httpResponse = await client
                .SendAsync(httpRequest, cancellationToken)
                .ConfigureAwait(false);

            if (httpResponse.IsSuccessStatusCode)
            {
                metrics.RecordRevocation(tenantId: null);
                LogRevocationSuccess(revocationEndpoint);
                return true;
            }

            LogRevocationFailure(revocationEndpoint, (int)httpResponse.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            LogRevocationHttpError(revocationEndpoint, ex.Message);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token revocation succeeded at {RevocationEndpoint}")]
    private partial void LogRevocationSuccess(string revocationEndpoint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token revocation failed at {RevocationEndpoint} with status code {StatusCode}")]
    private partial void LogRevocationFailure(string revocationEndpoint, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Revocation endpoint not available for authority {Authority} — skipping revocation")]
    private partial void LogRevocationEndpointNotAvailable(string authority);

    [LoggerMessage(Level = LogLevel.Error, Message = "HTTP error communicating with revocation endpoint {RevocationEndpoint}: {ErrorMessage}")]
    private partial void LogRevocationHttpError(string revocationEndpoint, string errorMessage);
}

#pragma warning restore GRSEC003
