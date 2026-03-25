using System.Net.Http.Headers;
using System.Text.Json;
using Granit.Bff.Diagnostics;
using Granit.Bff.Options;
using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.ClientAuthentication.Internal;
using Granit.Oidc.DPoP;
using Granit.Timing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Transforms;

namespace Granit.Bff.Yarp.Internal;

/// <summary>
/// YARP request transform that reads the BFF session cookie, loads tokens from
/// <see cref="IBffTokenStore"/>, and injects an <c>Authorization: Bearer</c> header.
/// Routes with <c>Granit.Bff.RequireAuth = true</c> metadata require a valid session;
/// unauthenticated requests receive a 401 response.
/// Uses <c>Granit.Bff.Frontend</c> metadata to determine which frontend's token to inject.
/// Includes automatic silent refresh when the token is about to expire.
/// </summary>
#pragma warning disable GRSEC003 // Class handles token injection — server-side only
internal sealed partial class BffTokenInjectionTransform(
    IBffTokenStore tokenStore,
    IOptions<GranitBffOptions> options,
    IHttpClientFactory httpClientFactory,
    IDPoPProofService dpopService,
    BffMetrics metrics,
    IClock clock,
    ILogger<BffTokenInjectionTransform> logger) : RequestTransform
{
    private const string RequireAuthMetadataKey = "Granit.Bff.RequireAuth";
    private const string FrontendMetadataKey = "Granit.Bff.Frontend";

    public override async ValueTask ApplyAsync(RequestTransformContext context)
    {
        HttpContext httpContext = context.HttpContext;

        (bool requiresAuth, string? frontendName) = ExtractRouteMetadata(httpContext);

        if (!requiresAuth)
        {
            return;
        }

        using System.Diagnostics.Activity? activity = BffActivitySource.Source.StartActivity(BffActivitySource.Proxy);
        metrics.RecordProxyRequest(null);

        GranitBffOptions bffOptions = options.Value;

        // Resolve frontend options from metadata
        BffFrontendOptions? frontend = ResolveFrontend(bffOptions, frontendName);
        if (frontend is null)
        {
            LogUnknownFrontend(logger, frontendName ?? "(null)");
            metrics.RecordProxyError(null, "unknown_frontend");
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        string? sessionId = httpContext.Request.Cookies[frontend.SessionCookieName];

        if (string.IsNullOrEmpty(sessionId))
        {
            LogMissingSession(logger, frontend.Name);
            metrics.RecordProxyError(null, "missing_session");
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        BffTokenSet? tokens = await tokenStore.GetAsync(frontend.Name, sessionId, httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (tokens is null)
        {
            LogExpiredSession(logger, sessionId, frontend.Name);
            metrics.RecordProxyError(null, "expired_session");
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Check if token needs refresh (expires within grace period)
        if (tokens.RefreshToken is not null
            && tokens.ExpiresAt - clock.Now < bffOptions.RefreshGracePeriod)
        {
            BffTokenSet? refreshed = await TryRefreshTokensAsync(
                bffOptions, frontend, tokens, httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (refreshed is not null)
            {
                tokens = refreshed;
                await tokenStore.StoreAsync(frontend.Name, sessionId, tokens, httpContext.RequestAborted)
                    .ConfigureAwait(false);

                metrics.RecordTokenRefresh(null);
                LogTokenRefreshed(logger, sessionId, frontend.Name);

                // Signal to the SPA that the session was refreshed
                httpContext.Response.Headers["X-Bff-Session-Refreshed"] = "true";
            }
            else
            {
                LogTokenRefreshFailed(logger, sessionId, frontend.Name);
                metrics.RecordProxyError(null, "refresh_failed");
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        InjectAuthorizationHeader(context, tokens);

        await ExtendSlidingSessionAsync(bffOptions, frontend, sessionId, tokens, httpContext.RequestAborted)
            .ConfigureAwait(false);
    }

    private static (bool RequiresAuth, string? FrontendName) ExtractRouteMetadata(HttpContext httpContext)
    {
        IReverseProxyFeature? proxyFeature = httpContext.Features.Get<IReverseProxyFeature>();
        if (proxyFeature?.Route.Config.Metadata is not { } metadata)
        {
            return (false, null);
        }

        bool requiresAuth = metadata.TryGetValue(RequireAuthMetadataKey, out string? requireAuthValue)
            && string.Equals(requireAuthValue, "true", StringComparison.OrdinalIgnoreCase);

        metadata.TryGetValue(FrontendMetadataKey, out string? frontendName);

        return (requiresAuth, frontendName);
    }

    private void InjectAuthorizationHeader(RequestTransformContext context, BffTokenSet tokens)
    {
        if (!string.IsNullOrEmpty(tokens.DPoPPrivateKeyJwk))
        {
            string targetUri = context.ProxyRequest.RequestUri?.GetLeftPart(UriPartial.Path)
                ?? context.HttpContext.Request.Path.Value ?? "/";
            string httpMethod = context.HttpContext.Request.Method;

            string dpopProof = dpopService.CreateProof(tokens.DPoPPrivateKeyJwk, httpMethod, targetUri, tokens.DPoPNonce);

            context.ProxyRequest.Headers.Authorization =
                new AuthenticationHeaderValue("DPoP", tokens.AccessToken);
            context.ProxyRequest.Headers.TryAddWithoutValidation("DPoP", dpopProof);
        }
        else
        {
            context.ProxyRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        }
    }

    private async Task ExtendSlidingSessionAsync(
        GranitBffOptions bffOptions,
        BffFrontendOptions frontend,
        string sessionId,
        BffTokenSet tokens,
        CancellationToken cancellationToken)
    {
        if (!bffOptions.UseSessionSlidingExpiration || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        DateTimeOffset now = clock.Now;
        DateTimeOffset halfwayPoint = tokens.SessionCreatedAt + (bffOptions.SessionDuration / 2);
        DateTimeOffset absoluteMax = tokens.SessionCreatedAt + bffOptions.SessionAbsoluteMaxDuration;

        // Only extend if past halfway and within absolute max — re-store resets the distributed cache TTL
        if (now >= halfwayPoint && now < absoluteMax)
        {
            await tokenStore.StoreAsync(frontend.Name, sessionId, tokens, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static BffFrontendOptions? ResolveFrontend(GranitBffOptions options, string? frontendName)
    {
        if (string.IsNullOrEmpty(frontendName))
        {
            // Fall back to the first frontend if only one is configured
            return options.Frontends.Count == 1 ? options.Frontends[0] : null;
        }

        return options.Frontends.Find(f =>
            string.Equals(f.Name, frontendName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<BffTokenSet?> TryRefreshTokensAsync(
        GranitBffOptions bffOptions,
        BffFrontendOptions frontend,
        BffTokenSet currentTokens,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpClient httpClient = httpClientFactory.CreateClient("Granit.Bff");
            string tokenEndpoint = $"{bffOptions.Authority.ToString().TrimEnd('/')}/connect/token";

            Dictionary<string, string> parameters = new()
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = currentTokens.RefreshToken!,
                ["client_id"] = frontend.ClientId,
            };

            ResolveClientAuth(frontend).Apply(parameters, frontend.ClientId, tokenEndpoint);

            using FormUrlEncodedContent content = new(parameters);
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint) { Content = content };

            // Attach DPoP proof for token refresh (RFC 9449 §5)
            if (!string.IsNullOrEmpty(currentTokens.DPoPPrivateKeyJwk))
            {
                string dpopProof = dpopService.CreateProof(
                    currentTokens.DPoPPrivateKeyJwk, "POST", tokenEndpoint, currentTokens.DPoPNonce);
                request.Headers.TryAddWithoutValidation("DPoP", dpopProof);
            }

            using HttpResponseMessage response = await httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            // Capture updated DPoP-Nonce from refresh response (RFC 9449 §8)
            string? dpopNonce = currentTokens.DPoPNonce;
            if (response.Headers.TryGetValues("DPoP-Nonce", out IEnumerable<string>? nonceValues))
            {
                dpopNonce = nonceValues.FirstOrDefault() ?? dpopNonce;
            }

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            JsonElement tokenResponse = await JsonSerializer.DeserializeAsync<JsonElement>(
                stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            string accessToken = tokenResponse.GetProperty("access_token").GetString()!;
            string? newRefreshToken = tokenResponse.TryGetProperty("refresh_token", out JsonElement rt)
                ? rt.GetString() : currentTokens.RefreshToken;
            string? idToken = tokenResponse.TryGetProperty("id_token", out JsonElement it) ? it.GetString() : null;
            int expiresIn = tokenResponse.TryGetProperty("expires_in", out JsonElement ei)
                ? ei.GetInt32() : 3600;

            return new BffTokenSet(
                accessToken,
                newRefreshToken,
                idToken,
                clock.Now.AddSeconds(expiresIn))
            {
                DPoPPrivateKeyJwk = currentTokens.DPoPPrivateKeyJwk,
                DPoPNonce = dpopNonce,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogTokenRefreshException(logger, ex, frontend.Name);
            return null;
        }
    }

    private IClientAuthenticationStrategy ResolveClientAuth(BffFrontendOptions frontend) =>
        frontend.ClientAuthenticationMethod == BffClientAuthenticationMethod.PrivateKeyJwt
            ? new PrivateKeyJwtStrategy(frontend.ClientSigningKeyJwk!, clock)
            : new ClientSecretPostStrategy(frontend.ClientSecret);

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF proxy: unknown frontend {FrontendName} in YARP route metadata")]
    private static partial void LogUnknownFrontend(ILogger logger, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF proxy: no session cookie present for frontend {FrontendName}")]
    private static partial void LogMissingSession(ILogger logger, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF proxy: session {SessionId} expired or not found for frontend {FrontendName}")]
    private static partial void LogExpiredSession(ILogger logger, string sessionId, string frontendName);

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF proxy: token refreshed for session {SessionId} on frontend {FrontendName}")]
    private static partial void LogTokenRefreshed(ILogger logger, string sessionId, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF proxy: token refresh failed for session {SessionId} on frontend {FrontendName}")]
    private static partial void LogTokenRefreshFailed(ILogger logger, string sessionId, string frontendName);

    [LoggerMessage(Level = LogLevel.Error, Message = "BFF proxy: token refresh failed with exception for frontend {FrontendName}")]
    private static partial void LogTokenRefreshException(ILogger logger, Exception exception, string frontendName);
}
#pragma warning restore GRSEC003
