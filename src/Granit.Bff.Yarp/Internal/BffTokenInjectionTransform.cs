using System.Net.Http.Headers;
using System.Text.Json;
using Granit.Bff.Diagnostics;
using Granit.Bff.Options;
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
/// Includes automatic silent refresh when the token is about to expire.
/// </summary>
#pragma warning disable GRSEC003 // Class handles token injection — server-side only
internal sealed partial class BffTokenInjectionTransform(
    IBffTokenStore tokenStore,
    IOptions<GranitBffOptions> options,
    IHttpClientFactory httpClientFactory,
    BffMetrics metrics,
    IClock clock,
    ILogger<BffTokenInjectionTransform> logger) : RequestTransform
{
    private const string RequireAuthMetadataKey = "Granit.Bff.RequireAuth";

    public override async ValueTask ApplyAsync(RequestTransformContext transformContext)
    {
        HttpContext httpContext = transformContext.HttpContext;

        // Check if route requires BFF auth via YARP feature
        bool requiresAuth = false;
        IReverseProxyFeature? proxyFeature = httpContext.Features.Get<IReverseProxyFeature>();
        if (proxyFeature?.Route.Config.Metadata is { } metadata
            && metadata.TryGetValue(RequireAuthMetadataKey, out string? requireAuthValue))
        {
            requiresAuth = string.Equals(requireAuthValue, "true", StringComparison.OrdinalIgnoreCase);
        }

        if (!requiresAuth)
        {
            return;
        }

        using System.Diagnostics.Activity? activity = BffActivitySource.Source.StartActivity(BffActivitySource.Proxy);
        metrics.RecordProxyRequest(null);

        GranitBffOptions bffOptions = options.Value;
        string? sessionId = httpContext.Request.Cookies[bffOptions.SessionCookieName];

        if (string.IsNullOrEmpty(sessionId))
        {
            LogMissingSession(logger);
            metrics.RecordProxyError(null, "missing_session");
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        BffTokenSet? tokens = await tokenStore.GetAsync(sessionId, httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (tokens is null)
        {
            LogExpiredSession(logger, sessionId);
            metrics.RecordProxyError(null, "expired_session");
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Check if token needs refresh (expires within grace period)
        if (tokens.RefreshToken is not null
            && tokens.ExpiresAt - clock.Now < bffOptions.RefreshGracePeriod)
        {
            BffTokenSet? refreshed = await TryRefreshTokensAsync(
                bffOptions, tokens.RefreshToken, httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (refreshed is not null)
            {
                tokens = refreshed;
                await tokenStore.StoreAsync(sessionId, tokens, httpContext.RequestAborted)
                    .ConfigureAwait(false);

                metrics.RecordTokenRefresh(null);
                LogTokenRefreshed(logger, sessionId);

                // Signal to the SPA that the session was refreshed
                httpContext.Response.Headers["X-Bff-Session-Refreshed"] = "true";
            }
            else
            {
                LogTokenRefreshFailed(logger, sessionId);
                metrics.RecordProxyError(null, "refresh_failed");
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        // Inject Bearer token
        transformContext.ProxyRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private async Task<BffTokenSet?> TryRefreshTokensAsync(
        GranitBffOptions bffOptions,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpClient httpClient = httpClientFactory.CreateClient("Granit.Bff");
            string tokenEndpoint = $"{bffOptions.Authority.ToString().TrimEnd('/')}/connect/token";

            Dictionary<string, string> parameters = new()
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = bffOptions.ClientId,
                ["client_secret"] = bffOptions.ClientSecret,
            };

            using FormUrlEncodedContent content = new(parameters);
            using HttpResponseMessage response = await httpClient
                .PostAsync(tokenEndpoint, content, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            JsonElement tokenResponse = await JsonSerializer.DeserializeAsync<JsonElement>(
                stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            string accessToken = tokenResponse.GetProperty("access_token").GetString()!;
            string? newRefreshToken = tokenResponse.TryGetProperty("refresh_token", out JsonElement rt)
                ? rt.GetString() : refreshToken;
            string? idToken = tokenResponse.TryGetProperty("id_token", out JsonElement it) ? it.GetString() : null;
            int expiresIn = tokenResponse.TryGetProperty("expires_in", out JsonElement ei)
                ? ei.GetInt32() : 3600;

            return new BffTokenSet(
                accessToken,
                newRefreshToken,
                idToken,
                clock.Now.AddSeconds(expiresIn));
        }
        catch (Exception)
        {
            return null;
        }
    }

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF proxy: no session cookie present")]
    private static partial void LogMissingSession(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF proxy: session {SessionId} expired or not found")]
    private static partial void LogExpiredSession(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF proxy: token refreshed for session {SessionId}")]
    private static partial void LogTokenRefreshed(ILogger logger, string sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF proxy: token refresh failed for session {SessionId}")]
    private static partial void LogTokenRefreshFailed(ILogger logger, string sessionId);
}
#pragma warning restore GRSEC003
