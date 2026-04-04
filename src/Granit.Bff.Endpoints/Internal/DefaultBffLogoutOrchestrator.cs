using Granit.Bff.Diagnostics;
using Granit.Bff.Options;
using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.ClientAuthentication.Internal;
using Granit.Oidc.DPoP;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Bff.Endpoints.Internal;

/// <summary>
/// Default implementation of <see cref="IBffLogoutOrchestrator"/>. Handles token revocation,
/// session removal, and end_session URL construction.
/// </summary>
internal sealed partial class DefaultBffLogoutOrchestrator(
    IOptions<GranitBffOptions> options,
    IBffTokenStore tokenStore,
    IHttpClientFactory httpClientFactory,
    IDPoPProofService dpopService,
    IClock clock,
    BffMetrics metrics,
    ILogger<DefaultBffLogoutOrchestrator> logger) : IBffLogoutOrchestrator
{
    private readonly GranitBffOptions _bffOptions = options.Value;

    /// <inheritdoc/>
    public async Task<string?> RevokeSessionAsync(
        string frontendName,
        string sessionId,
        CancellationToken cancellationToken)
    {
        string? idTokenHint = null;

#pragma warning disable GRSEC003 // Variable handles tokens — server-side only
        BffTokenSet? tokens = await tokenStore.GetAsync(frontendName, sessionId, cancellationToken)
            .ConfigureAwait(false);
        idTokenHint = tokens?.IdToken;

        if (tokens is not null)
        {
            await RevokeTokensAsync(frontendName, tokens, cancellationToken).ConfigureAwait(false);
        }
#pragma warning restore GRSEC003

        await tokenStore.RemoveAsync(frontendName, sessionId, cancellationToken).ConfigureAwait(false);
        metrics.RecordLogout(null);
        LogLogout(logger, sessionId, frontendName);

        return idTokenHint;
    }

    /// <inheritdoc/>
    public string BuildEndSessionUrl(
        BffFrontendOptions frontend,
        string postLogoutRedirectUri,
        string? idTokenHint)
    {
#pragma warning disable GRSEC003 // Building OIDC end_session URL with client credentials
        string endSessionUrl = $"{_bffOptions.Authority.ToString().TrimEnd('/')}/connect/logout"
            + $"?post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}"
            + $"&client_id={Uri.EscapeDataString(frontend.ClientId)}";
#pragma warning restore GRSEC003

        if (!string.IsNullOrEmpty(idTokenHint))
        {
#pragma warning disable GRSEC003 // Appending id_token_hint — standard OIDC parameter
            endSessionUrl += $"&id_token_hint={Uri.EscapeDataString(idTokenHint)}";
#pragma warning restore GRSEC003
        }

        return endSessionUrl;
    }

    /// <summary>
    /// Best-effort token revocation (RFC 7009). Revokes refresh token first (cascades
    /// in OpenIddict), then access token. Failures are logged but do not block logout.
    /// </summary>
#pragma warning disable GRSEC003 // Method handles tokens for revocation — server-side only
    private async Task RevokeTokensAsync(
        string frontendName,
        BffTokenSet tokens,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpClient httpClient = httpClientFactory.CreateClient("Granit.Bff");
            string revokeEndpoint = $"{_bffOptions.Authority.ToString().TrimEnd('/')}/connect/revoke";

            BffFrontendOptions frontend = _bffOptions.Frontends.First(f => f.Name == frontendName);

            // Revoke refresh token first (cascades to access token in OpenIddict)
            if (!string.IsNullOrEmpty(tokens.RefreshToken))
            {
                await RevokeTokenAsync(
                    httpClient, revokeEndpoint, tokens.RefreshToken, "refresh_token",
                    frontend, tokens, cancellationToken).ConfigureAwait(false);
            }

            // Explicitly revoke access token as well (defense in depth)
            await RevokeTokenAsync(
                httpClient, revokeEndpoint, tokens.AccessToken, "access_token",
                frontend, tokens, cancellationToken).ConfigureAwait(false);

            LogTokensRevoked(logger, frontendName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Best-effort — logout must not fail because of revocation errors
            LogRevocationFailed(logger, ex, frontendName);
        }
    }

    private async Task RevokeTokenAsync(
        HttpClient httpClient,
        string revokeEndpoint,
        string token,
        string tokenTypeHint,
        BffFrontendOptions frontend,
        BffTokenSet tokens,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> parameters = new()
        {
            ["token"] = token,
            ["token_type_hint"] = tokenTypeHint,
            ["client_id"] = frontend.ClientId,
        };

        ResolveClientAuth(frontend).Apply(parameters, frontend.ClientId, revokeEndpoint);

        using FormUrlEncodedContent content = new(parameters);
        using var request = new HttpRequestMessage(HttpMethod.Post, revokeEndpoint) { Content = content };

        // Attach DPoP proof if active
        if (!string.IsNullOrEmpty(tokens.DPoPPrivateKeyJwk))
        {
            string dpopProof = dpopService.CreateProof(tokens.DPoPPrivateKeyJwk, "POST", revokeEndpoint, tokens.DPoPNonce);
            request.Headers.TryAddWithoutValidation("DPoP", dpopProof);
        }

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        // RFC 7009: server returns 200 on success, any other status is logged but not fatal
    }
#pragma warning restore GRSEC003

    private IClientAuthenticationStrategy ResolveClientAuth(BffFrontendOptions frontend) =>
        frontend.ClientAuthenticationMethod == BffClientAuthenticationMethod.PrivateKeyJwt
            ? new PrivateKeyJwtStrategy(frontend.ClientSigningKeyJwk!, clock)
            : new ClientSecretPostStrategy(frontend.ClientSecret);

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF logout: session {SessionId} cleared for frontend {FrontendName}")]
    private static partial void LogLogout(ILogger logger, string sessionId, string frontendName);

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF logout: tokens revoked at authorization server for frontend {FrontendName}")]
    private static partial void LogTokensRevoked(ILogger logger, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF logout: token revocation failed for frontend {FrontendName} (best-effort, logout continues)")]
    private static partial void LogRevocationFailed(ILogger logger, Exception exception, string frontendName);
}
