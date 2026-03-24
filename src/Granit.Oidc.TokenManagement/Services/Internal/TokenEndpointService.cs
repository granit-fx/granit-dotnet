using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.Discovery;
using Granit.Oidc.DPoP;
using Granit.Oidc.Internal;
using Granit.Oidc.Requests;
using Granit.Oidc.Responses;
using Granit.Oidc.TokenManagement.Diagnostics;
using Microsoft.Extensions.Logging;

#pragma warning disable GRSEC003 // Service handles OAuth 2.0 token endpoint communication including access tokens and DPoP proofs

namespace Granit.Oidc.TokenManagement.Services.Internal;

/// <summary>
/// Sends requests to an OAuth 2.0 token endpoint, handling discovery resolution,
/// client authentication, DPoP proof generation, and automatic DPoP nonce retry.
/// </summary>
internal sealed partial class TokenEndpointService(
    IDiscoveryDocumentService discoveryDocumentService,
    IHttpClientFactory httpClientFactory,
    IDPoPProofService dpopProofService,
    TokenManagementMetrics metrics,
    ILogger<TokenEndpointService> logger) : ITokenEndpointService
{
    private const string HttpClientName = "Granit.TokenManagement";
    private const string DPoPHeader = "DPoP";
    private const string DPoPNonceHeader = "DPoP-Nonce";
    private const string UseDPoPNonceError = "use_dpop_nonce";

    /// <inheritdoc/>
    public async Task<TokenResponse> RequestTokenAsync(
        string authority,
        TokenRequest request,
        IClientAuthenticationStrategy? clientAuth = null,
        DPoPOptions? dpop = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(authority);
        ArgumentNullException.ThrowIfNull(request);

        using Activity? activity = TokenManagementActivitySource.Source.StartActivity(
            TokenManagementActivitySource.RequestToken);
        activity?.SetTag("authority", authority);
        activity?.SetTag("client_id", request.ClientId);

        OidcDiscoveryDocument disco = await discoveryDocumentService
            .GetAsync(authority, cancellationToken)
            .ConfigureAwait(false);

        string tokenEndpoint = disco.TokenEndpoint;

        Dictionary<string, string> parameters = TokenRequestEncoder.ToParameters(request);
        clientAuth?.Apply(parameters, request.ClientId, tokenEndpoint);

        string grantType = parameters.GetValueOrDefault("grant_type") ?? "unknown";

        TokenResponse response = await SendTokenRequestAsync(
            tokenEndpoint, parameters, dpop, cancellationToken).ConfigureAwait(false);

        // DPoP nonce retry: if the server requires a nonce, rebuild the proof and retry once
        if (!response.IsSuccess
            && response.Error.Error == UseDPoPNonceError
            && dpop is not null
            && response.DPoPNonce is not null)
        {
            LogDPoPNonceRetry(tokenEndpoint, response.DPoPNonce);

            DPoPOptions retryDpop = dpop with { Nonce = response.DPoPNonce };
            response = await SendTokenRequestAsync(
                tokenEndpoint, parameters, retryDpop, cancellationToken).ConfigureAwait(false);
        }

        if (response.IsSuccess)
        {
            metrics.RecordTokenRequest(tenantId: null, grantType, request.ClientId);
            LogTokenRequestSuccess(tokenEndpoint, grantType);
        }
        else
        {
            metrics.RecordError(tenantId: null, response.Error.Error);
            LogTokenRequestFailure(tokenEndpoint, grantType, response.Error.Error, response.Error.ErrorDescription);
        }

        return response;
    }

    private async Task<TokenResponse> SendTokenRequestAsync(
        string tokenEndpoint,
        Dictionary<string, string> parameters,
        DPoPOptions? dpop,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(parameters),
        };

        if (dpop is not null)
        {
            string proof = dpopProofService.CreateProof(
                dpop.PrivateKeyJwk, "POST", tokenEndpoint, dpop.Nonce);
            httpRequest.Headers.TryAddWithoutValidation(DPoPHeader, proof);
        }

        HttpClient client = httpClientFactory.CreateClient(HttpClientName);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogHttpError(tokenEndpoint, ex.Message);
            return TokenResponse.FromError("http_error", $"Token endpoint request failed: {ex.Message}");
        }

        string? dpopNonce = httpResponse.Headers.TryGetValues(DPoPNonceHeader, out IEnumerable<string>? nonces)
            ? nonces.FirstOrDefault()
            : null;

        using Stream responseStream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using JsonDocument doc = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return TokenResponse.FromJson(doc.RootElement, dpopNonce);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Token request succeeded for endpoint {TokenEndpoint} with grant type {GrantType}")]
    private partial void LogTokenRequestSuccess(string tokenEndpoint, string grantType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token request failed for endpoint {TokenEndpoint} with grant type {GrantType}: {Error} — {ErrorDescription}")]
    private partial void LogTokenRequestFailure(string tokenEndpoint, string grantType, string error, string? errorDescription);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DPoP nonce retry for endpoint {TokenEndpoint} with nonce {Nonce}")]
    private partial void LogDPoPNonceRetry(string tokenEndpoint, string nonce);

    [LoggerMessage(Level = LogLevel.Error, Message = "HTTP error communicating with token endpoint {TokenEndpoint}: {ErrorMessage}")]
    private partial void LogHttpError(string tokenEndpoint, string errorMessage);
}

#pragma warning restore GRSEC003
