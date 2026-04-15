using System.Text.Json;
using Granit.Bff.Diagnostics;
using Granit.Bff.Options;
using Granit.Http.Cookies;
using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.ClientAuthentication.Internal;
using Granit.Oidc.DPoP;
using Granit.Timing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Bff.Endpoints.Extensions;

/// <summary>
/// ASP.NET Core middleware that injects BFF session tokens into downstream requests.
/// Alternative to YARP-based <c>BffTokenInjectionTransform</c> for self-hosted deployments
/// where the API runs in the same process as the BFF.
/// </summary>
/// <remarks>
/// <para>
/// Intercepts requests matching a configurable path prefix (default: <c>/api</c>),
/// resolves the BFF frontend from session cookies, validates CSRF on mutating methods,
/// performs silent token refresh when near expiry, and injects an
/// <c>Authorization</c> header. Includes sliding session extension and metrics.
/// </para>
/// <para>
/// Requests without a BFF session cookie pass through unmodified, allowing the API
/// to accept other authentication methods (JWT bearer, API keys).
/// </para>
/// </remarks>
#pragma warning disable GRSEC003 // Middleware handles tokens — server-side only
public sealed partial class BffTokenInjectionMiddleware
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post, HttpMethods.Put, HttpMethods.Delete, HttpMethods.Patch,
    };

    private readonly RequestDelegate _next;
    private readonly PathString _pathPrefix;

    /// <summary>
    /// Initializes a new instance of <see cref="BffTokenInjectionMiddleware"/>.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="pathPrefix">Request path prefix to intercept (e.g. <c>/api</c>).</param>
    public BffTokenInjectionMiddleware(RequestDelegate next, string pathPrefix)
    {
        _next = next;
        _pathPrefix = new PathString(pathPrefix);
    }

    /// <summary>Processes an HTTP request.</summary>
    public async Task InvokeAsync(
        HttpContext context,
        IOptions<GranitBffOptions> options,
        IBffTokenStore tokenStore,
        IBffCsrfTokenGenerator csrfGenerator,
        IGranitCookieManager cookieManager,
        BffMetrics metrics,
        IClock clock,
        ILogger<BffTokenInjectionMiddleware> logger)
    {
        if (!context.Request.Path.StartsWithSegments(_pathPrefix))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        GranitBffOptions bffOptions = options.Value;

        // Resolve frontend from session cookies
        (BffFrontendOptions? frontend, string? sessionId) = ResolveFrontendFromCookies(context, bffOptions);

        if (frontend is null || string.IsNullOrEmpty(sessionId))
        {
            // No BFF session — continue without injection (API may accept other auth methods)
            await _next(context).ConfigureAwait(false);
            return;
        }

        using System.Diagnostics.Activity? activity = BffActivitySource.Source.StartActivity(BffActivitySource.Proxy);
        metrics.RecordProxyRequest(null);

        // CSRF validation on mutating methods
        if (MutatingMethods.Contains(context.Request.Method))
        {
            string? csrfToken = context.Request.Headers["X-CSRF-Token"].FirstOrDefault();
            if (string.IsNullOrEmpty(csrfToken) || !csrfGenerator.Validate(sessionId, csrfToken))
            {
                LogCsrfRejection(logger, context.Request.Method, context.Request.Path, frontend.Name);
                metrics.RecordCsrfRejection(null);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        // Load tokens
        BffTokenSet? tokens = await tokenStore.GetAsync(frontend.Name, sessionId, context.RequestAborted)
            .ConfigureAwait(false);

        if (tokens is null)
        {
            // Session no longer exists (server restart, cache eviction, expiry).
            // Clear the stale cookie so subsequent requests don't repeat this path,
            // then continue as unauthenticated — the endpoint's own authorization
            // policy decides whether to challenge or allow the request.
            LogExpiredSession(logger, MaskSessionId(sessionId), frontend.Name);
            metrics.RecordProxyError(null, "expired_session");
            cookieManager.DeleteCookie(context, frontend.SessionCookieName);
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Silent refresh if near expiry
        if (tokens.RefreshToken is not null
            && tokens.ExpiresAt - clock.Now < bffOptions.RefreshGracePeriod)
        {
            BffTokenSet? refreshed = await TryRefreshTokensAsync(
                context, bffOptions, frontend, tokens, clock, logger)
                .ConfigureAwait(false);

            if (refreshed is not null)
            {
                tokens = refreshed;
                await tokenStore.StoreAsync(frontend.Name, sessionId, tokens, context.RequestAborted)
                    .ConfigureAwait(false);
                metrics.RecordTokenRefresh(null);
                LogTokenRefreshed(logger, MaskSessionId(sessionId), frontend.Name);
                context.Response.Headers["X-Bff-Session-Refreshed"] = "true";
            }
            else
            {
                // Refresh failed — clear the stale session and continue as unauthenticated.
                LogTokenRefreshFailed(logger, MaskSessionId(sessionId), frontend.Name);
                metrics.RecordProxyError(null, "refresh_failed");
                cookieManager.DeleteCookie(context, frontend.SessionCookieName);
                await _next(context).ConfigureAwait(false);
                return;
            }
        }

        // Inject Authorization header
        InjectAuthorizationHeader(context, tokens);

        // Extend sliding session
        await ExtendSlidingSessionAsync(
            bffOptions, frontend, sessionId, tokens, tokenStore, clock, context.RequestAborted)
            .ConfigureAwait(false);

        await _next(context).ConfigureAwait(false);
    }

    private static (BffFrontendOptions? Frontend, string? SessionId) ResolveFrontendFromCookies(
        HttpContext context, GranitBffOptions options)
    {
        foreach (BffFrontendOptions frontend in options.Frontends)
        {
            string? sessionId = context.Request.Cookies[frontend.SessionCookieName];
            if (!string.IsNullOrEmpty(sessionId))
            {
                return (frontend, sessionId);
            }
        }

        return (null, null);
    }

    private static void InjectAuthorizationHeader(HttpContext context, BffTokenSet tokens)
    {
        if (!string.IsNullOrEmpty(tokens.DPoPPrivateKeyJwk))
        {
            IDPoPProofService dpopService = context.RequestServices.GetRequiredService<IDPoPProofService>();
            string targetUri = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}";
            string dpopProof = dpopService.CreateProof(
                tokens.DPoPPrivateKeyJwk, context.Request.Method, targetUri, tokens.DPoPNonce);

            context.Request.Headers.Authorization = $"DPoP {tokens.AccessToken}";
            context.Request.Headers.Append("DPoP", dpopProof);
        }
        else
        {
            context.Request.Headers.Authorization = $"Bearer {tokens.AccessToken}";
        }
    }

    private static async Task ExtendSlidingSessionAsync(
        GranitBffOptions bffOptions,
        BffFrontendOptions frontend,
        string sessionId,
        BffTokenSet tokens,
        IBffTokenStore tokenStore,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!bffOptions.UseSessionSlidingExpiration)
        {
            return;
        }

        DateTimeOffset now = clock.Now;
        DateTimeOffset halfwayPoint = tokens.SessionCreatedAt + (bffOptions.SessionDuration / 2);
        DateTimeOffset absoluteMax = tokens.SessionCreatedAt + bffOptions.SessionAbsoluteMaxDuration;

        if (now >= halfwayPoint && now < absoluteMax)
        {
            await tokenStore.StoreAsync(frontend.Name, sessionId, tokens, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task<BffTokenSet?> TryRefreshTokensAsync(
        HttpContext context,
        GranitBffOptions bffOptions,
        BffFrontendOptions frontend,
        BffTokenSet currentTokens,
        IClock clock,
        ILogger logger)
    {
        try
        {
            IHttpClientFactory httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
            IDPoPProofService dpopService = context.RequestServices.GetRequiredService<IDPoPProofService>();

            using HttpClient httpClient = httpClientFactory.CreateClient("Granit.Bff");
            string tokenEndpoint = $"{bffOptions.Authority.ToString().TrimEnd('/')}/connect/token";

            Dictionary<string, string> parameters = new()
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = currentTokens.RefreshToken!,
                ["client_id"] = frontend.ClientId,
            };

            ResolveClientAuth(frontend, clock).Apply(parameters, frontend.ClientId, tokenEndpoint);

            using FormUrlEncodedContent content = new(parameters);
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint) { Content = content };

            if (!string.IsNullOrEmpty(currentTokens.DPoPPrivateKeyJwk))
            {
                string dpopProof = dpopService.CreateProof(
                    currentTokens.DPoPPrivateKeyJwk, "POST", tokenEndpoint, currentTokens.DPoPNonce);
                request.Headers.TryAddWithoutValidation("DPoP", dpopProof);
            }

            using HttpResponseMessage response = await httpClient
                .SendAsync(request, context.RequestAborted)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string? dpopNonce = currentTokens.DPoPNonce;
            if (response.Headers.TryGetValues("DPoP-Nonce", out IEnumerable<string>? nonceValues))
            {
                dpopNonce = nonceValues.FirstOrDefault() ?? dpopNonce;
            }

            using Stream stream = await response.Content.ReadAsStreamAsync(context.RequestAborted)
                .ConfigureAwait(false);

            JsonElement tokenResponse = await JsonSerializer.DeserializeAsync<JsonElement>(
                stream, cancellationToken: context.RequestAborted)
                .ConfigureAwait(false);

            string accessToken = tokenResponse.GetProperty("access_token").GetString()!;
            string? newRefreshToken = tokenResponse.TryGetProperty("refresh_token", out JsonElement rt)
                ? rt.GetString() : currentTokens.RefreshToken;
            string? idToken = tokenResponse.TryGetProperty("id_token", out JsonElement it)
                ? it.GetString() : null;
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
                SessionCreatedAt = currentTokens.SessionCreatedAt,
                UserId = currentTokens.UserId,
                UserAgent = currentTokens.UserAgent,
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

    private static IClientAuthenticationStrategy ResolveClientAuth(BffFrontendOptions frontend, IClock clock) =>
        frontend.ClientAuthenticationMethod == BffClientAuthenticationMethod.PrivateKeyJwt
            ? new PrivateKeyJwtStrategy(frontend.ClientSigningKeyJwk!, clock)
            : new ClientSecretPostStrategy(frontend.ClientSecret);

    private static string MaskSessionId(string sessionId) =>
        sessionId.Length > 8 ? $"{sessionId[..4]}...{sessionId[^4..]}" : "****";

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "BFF middleware: CSRF validation failed for {Method} {Path} on frontend {FrontendName}")]
    private static partial void LogCsrfRejection(ILogger logger, string method, string path, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "BFF middleware: session {SessionId} expired or not found for frontend {FrontendName}")]
    private static partial void LogExpiredSession(ILogger logger, string sessionId, string frontendName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "BFF middleware: token refreshed for session {SessionId} on frontend {FrontendName}")]
    private static partial void LogTokenRefreshed(ILogger logger, string sessionId, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "BFF middleware: token refresh failed for session {SessionId} on frontend {FrontendName}")]
    private static partial void LogTokenRefreshFailed(ILogger logger, string sessionId, string frontendName);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "BFF middleware: token refresh failed with exception for frontend {FrontendName}")]
    private static partial void LogTokenRefreshException(ILogger logger, Exception exception, string frontendName);
}
#pragma warning restore GRSEC003
