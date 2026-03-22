using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Bff.Diagnostics;
using Granit.Bff.DPoP;
using Granit.Bff.Options;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Bff.Endpoints.Endpoints;

/// <summary>
/// BFF login and callback endpoints. Implements OIDC authorization code flow with PKCE.
/// Registered per-frontend under <c>/{pathPrefix}/bff/login</c>.
/// </summary>
internal static partial class BffLoginEndpoints
{
    private const string PkceKeyPrefix = "bff:pkce:";
    private const int CodeVerifierLength = 64;

    internal static RouteGroupBuilder MapLoginEndpoints(this RouteGroupBuilder group, BffFrontendOptions frontend)
    {
        group.MapGet("/login", (HttpContext httpContext,
                [FromServices] IOptions<GranitBffOptions> options,
                [FromServices] IDistributedCache cache,
                [FromServices] IClock clock,
                [FromServices] IHttpClientFactory httpClientFactory,
                [FromServices] IBffDPoPService dpopService,
                [FromServices] ILoggerFactory loggerFactory) =>
                HandleLoginAsync(httpContext, frontend, options, cache, clock, httpClientFactory, dpopService, loggerFactory))
            .WithName($"BffLogin_{frontend.Name}")
            .WithSummary("Initiates OIDC login with PKCE and redirects to the authority.")
            .WithDescription(
                "Generates a PKCE code verifier and challenge, stores the verifier in the "
                + "distributed cache, and returns a 302 redirect to the OIDC authorization endpoint. "
                + "The callback endpoint exchanges the authorization code for tokens.")
            .Produces(StatusCodes.Status302Found)
            .ExcludeFromDescription();

        group.MapGet("/callback", (HttpContext httpContext,
                [FromQuery] string? code,
                [FromQuery] string? state,
                [FromQuery] string? error,
                [FromServices] IOptions<GranitBffOptions> options,
                [FromServices] IDistributedCache cache,
                [FromServices] IBffTokenStore tokenStore,
                [FromServices] BffMetrics metrics,
                [FromServices] IClock clock,
                [FromServices] IHttpClientFactory httpClientFactory,
                [FromServices] IBffDPoPService dpopService,
                [FromServices] ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                HandleCallbackAsync(httpContext, frontend, code, state, error, options, cache,
                    tokenStore, metrics, clock, httpClientFactory, dpopService, loggerFactory, cancellationToken))
            .WithName($"BffCallback_{frontend.Name}")
            .WithSummary("Handles the OIDC callback, exchanges the code for tokens, and sets the session cookie.")
            .WithDescription(
                "Receives the authorization code from the OIDC provider, exchanges it for tokens "
                + "using the stored PKCE code verifier, stores the tokens server-side, sets a session "
                + "cookie, and redirects to the configured post-login path. Returns 400 if the "
                + "authorization code or state is missing or invalid.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ExcludeFromDescription();

        return group;
    }

#pragma warning disable GRAPI003 // Private handler — not a direct endpoint delegate; services are resolved via lambda
    private static async Task<Results<RedirectHttpResult, ProblemHttpResult>> HandleLoginAsync(
        HttpContext httpContext,
        BffFrontendOptions frontend,
        [FromServices] IOptions<GranitBffOptions> options,
        [FromServices] IDistributedCache cache,
        [FromServices] IClock clock,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] IBffDPoPService dpopService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        ILogger logger = loggerFactory.CreateLogger("Granit.Bff.Endpoints.BffLoginEndpoints");
        Activity? activity = BffActivitySource.Source.StartActivity(BffActivitySource.Login);
        using IDisposable? activityScope = activity;

        GranitBffOptions bffOptions = options.Value;

        // Generate PKCE code verifier and challenge
        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = ComputeCodeChallenge(codeVerifier);

        // Generate state parameter to correlate callback
        string state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        // Generate DPoP key pair if enabled (stored alongside PKCE state, used during token exchange)
        string? dpopPrivateKeyJwk = frontend.UseDPoP ? dpopService.GenerateKeyPair() : null;

        // Store code_verifier + state + frontend name + DPoP key in distributed cache (short TTL for the auth round-trip)
        PkceState pkceData = new(codeVerifier, state, frontend.Name, dpopPrivateKeyJwk);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(pkceData);
        DistributedCacheEntryOptions cacheOptions = new()
        {
            AbsoluteExpiration = clock.Now.AddMinutes(10),
        };

        await cache.SetAsync($"{PkceKeyPrefix}{state}", json, cacheOptions)
            .ConfigureAwait(false);

        // Build authorization URL
#pragma warning disable GRSEC003 // Building OIDC authorize URL with client credentials
        string scopes = string.Join(" ", frontend.Scopes);
        string pathPrefix = string.IsNullOrEmpty(frontend.PathPrefix) ? "" : frontend.PathPrefix;
        string callbackUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{pathPrefix}/bff/callback";
        string authorityBase = bffOptions.Authority.ToString().TrimEnd('/');

        string authorizeUrl;

        if (frontend.UsePushedAuthorizationRequests)
        {
            // PAR: push parameters to /connect/par, then redirect with request_uri only
            string? requestUri = await PushAuthorizationRequestAsync(
                httpClientFactory, authorityBase, frontend, callbackUrl, scopes, state,
                codeChallenge, logger).ConfigureAwait(false);

            if (requestUri is not null)
            {
                authorizeUrl = $"{authorityBase}/connect/authorize"
                    + $"?client_id={Uri.EscapeDataString(frontend.ClientId)}"
                    + $"&request_uri={Uri.EscapeDataString(requestUri)}";
            }
            else
            {
                // PAR failed — fall back to direct parameters
                LogParFallback(logger, frontend.Name);
                authorizeUrl = BuildDirectAuthorizeUrl(
                    authorityBase, frontend.ClientId, callbackUrl, scopes, state, codeChallenge);
            }
        }
        else
        {
            authorizeUrl = BuildDirectAuthorizeUrl(
                authorityBase, frontend.ClientId, callbackUrl, scopes, state, codeChallenge);
        }
#pragma warning restore GRSEC003

        LogLoginRedirect(logger, bffOptions.Authority.ToString(), frontend.Name);

        return TypedResults.Redirect(authorizeUrl);
    }

    private static async Task<Results<RedirectHttpResult, ProblemHttpResult>> HandleCallbackAsync(
        HttpContext httpContext,
        BffFrontendOptions frontend,
        string? code,
        string? state,
        string? error,
        [FromServices] IOptions<GranitBffOptions> options,
        [FromServices] IDistributedCache cache,
        [FromServices] IBffTokenStore tokenStore,
        [FromServices] BffMetrics metrics,
        [FromServices] IClock clock,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] IBffDPoPService dpopService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger("Granit.Bff.Endpoints.BffLoginEndpoints");
        Activity? activity = BffActivitySource.Source.StartActivity(BffActivitySource.Callback);
        using IDisposable? activityScope = activity;

        if (!string.IsNullOrEmpty(error))
        {
            LogCallbackError(logger, error, frontend.Name);
            return TypedResults.Problem(
                detail: $"OIDC authorization error: {error}",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return TypedResults.Problem(
                detail: "Missing authorization code or state parameter.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        GranitBffOptions bffOptions = options.Value;

        // Retrieve and remove PKCE state
        string pkceKey = $"{PkceKeyPrefix}{state}";
        byte[]? pkceBytes = await cache.GetAsync(pkceKey, cancellationToken).ConfigureAwait(false);
        if (pkceBytes is null)
        {
            return TypedResults.Problem(
                detail: "Invalid or expired state parameter.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await cache.RemoveAsync(pkceKey, cancellationToken).ConfigureAwait(false);

        PkceState? pkceState = JsonSerializer.Deserialize<PkceState>(pkceBytes);
        if (pkceState is null)
        {
            return TypedResults.Problem(
                detail: "Corrupted PKCE state.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Exchange authorization code for tokens
        string pathPrefix = string.IsNullOrEmpty(frontend.PathPrefix) ? "" : frontend.PathPrefix;
        string callbackUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{pathPrefix}/bff/callback";
        BffTokenSet? tokens = await ExchangeCodeForTokensAsync(
            httpClientFactory, bffOptions, frontend, code, pkceState.CodeVerifier, callbackUrl,
            pkceState.DPoPPrivateKeyJwk, dpopService, clock, cancellationToken)
            .ConfigureAwait(false);

        if (tokens is null)
        {
            LogTokenExchangeFailed(logger, frontend.Name);
            return TypedResults.Problem(
                detail: "Failed to exchange authorization code for tokens.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Generate session ID and store tokens — random, not DB-stored, so sequential GUIDs are not needed
#pragma warning disable GRSEC002 // Session IDs are ephemeral cache keys, not clustered index values
        string sessionId = Guid.NewGuid().ToString("N");
#pragma warning restore GRSEC002
        await tokenStore.StoreAsync(frontend.Name, sessionId, tokens, cancellationToken).ConfigureAwait(false);

        // Set frontend-specific session cookie
        httpContext.Response.Cookies.Append(frontend.SessionCookieName, sessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = bffOptions.SessionDuration,
            IsEssential = true,
        });

        metrics.RecordLogin(null);
        LogLoginSuccess(logger, sessionId, frontend.Name);

        return TypedResults.Redirect(frontend.EffectivePostLoginRedirectPath);
    }
#pragma warning restore GRAPI003

#pragma warning disable GRSEC003 // Method handles tokens — server-side only
    private static async Task<BffTokenSet?> ExchangeCodeForTokensAsync(
        IHttpClientFactory httpClientFactory,
        GranitBffOptions options,
        BffFrontendOptions frontend,
        string code,
        string codeVerifier,
        string redirectUri,
        string? dpopPrivateKeyJwk,
        IBffDPoPService dpopService,
        IClock clock,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = httpClientFactory.CreateClient("Granit.Bff");
        string tokenEndpoint = $"{options.Authority.ToString().TrimEnd('/')}/connect/token";

        Dictionary<string, string> parameters = new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = frontend.ClientId,
            ["client_secret"] = frontend.ClientSecret,
            ["code_verifier"] = codeVerifier,
        };

        using FormUrlEncodedContent content = new(parameters);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint) { Content = content };

        // Attach DPoP proof if key is available (RFC 9449 §4)
        if (!string.IsNullOrEmpty(dpopPrivateKeyJwk))
        {
            string dpopProof = dpopService.CreateProof(dpopPrivateKeyJwk, "POST", tokenEndpoint);
            request.Headers.TryAddWithoutValidation("DPoP", dpopProof);
        }

        using HttpResponseMessage response = await httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        JsonElement tokenResponse = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        string accessToken = tokenResponse.GetProperty("access_token").GetString()!;
        string? refreshToken = tokenResponse.TryGetProperty("refresh_token", out JsonElement rt) ? rt.GetString() : null;
        string? idToken = tokenResponse.TryGetProperty("id_token", out JsonElement it) ? it.GetString() : null;
        int expiresIn = tokenResponse.TryGetProperty("expires_in", out JsonElement ei) ? ei.GetInt32() : 3600;

        return new BffTokenSet(
            accessToken,
            refreshToken,
            idToken,
            clock.Now.AddSeconds(expiresIn))
        {
            DPoPPrivateKeyJwk = dpopPrivateKeyJwk,
        };
    }
#pragma warning restore GRSEC003

    private static string BuildDirectAuthorizeUrl(
        string authorityBase, string clientId, string callbackUrl,
        string scopes, string state, string codeChallenge) =>
        $"{authorityBase}/connect/authorize"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&response_type=code"
            + $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}"
            + $"&scope={Uri.EscapeDataString(scopes)}"
            + $"&state={Uri.EscapeDataString(state)}"
            + $"&code_challenge={Uri.EscapeDataString(codeChallenge)}"
            + $"&code_challenge_method=S256";

#pragma warning disable GRSEC003 // Method handles client credentials for PAR — server-side only
    private static async Task<string?> PushAuthorizationRequestAsync(
        IHttpClientFactory httpClientFactory,
        string authorityBase,
        BffFrontendOptions frontend,
        string callbackUrl,
        string scopes,
        string state,
        string codeChallenge,
        ILogger logger)
    {
        try
        {
            using HttpClient httpClient = httpClientFactory.CreateClient("Granit.Bff");
            string parEndpoint = $"{authorityBase}/connect/par";

            Dictionary<string, string> parameters = new()
            {
                ["client_id"] = frontend.ClientId,
                ["client_secret"] = frontend.ClientSecret,
                ["response_type"] = "code",
                ["redirect_uri"] = callbackUrl,
                ["scope"] = scopes,
                ["state"] = state,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256",
            };

            using FormUrlEncodedContent content = new(parameters);
            using HttpResponseMessage response = await httpClient
                .PostAsync(parEndpoint, content)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                LogParRequestFailed(logger, (int)response.StatusCode, frontend.Name);
                return null;
            }

            using Stream stream = await response.Content.ReadAsStreamAsync()
                .ConfigureAwait(false);

            JsonElement parResponse = await JsonSerializer.DeserializeAsync<JsonElement>(stream)
                .ConfigureAwait(false);

            string? requestUri = parResponse.TryGetProperty("request_uri", out JsonElement ru)
                ? ru.GetString() : null;

            if (string.IsNullOrEmpty(requestUri))
            {
                LogParMissingRequestUri(logger, frontend.Name);
                return null;
            }

            LogParSuccess(logger, frontend.Name);
            return requestUri;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogParException(logger, ex, frontend.Name);
            return null;
        }
    }
#pragma warning restore GRSEC003

    private static string GenerateCodeVerifier()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(CodeVerifierLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string ComputeCodeChallenge(string codeVerifier)
    {
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private sealed record PkceState(string CodeVerifier, string State, string FrontendName, string? DPoPPrivateKeyJwk = null);

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF login: redirecting to authority {Authority} for frontend {FrontendName}")]
    private static partial void LogLoginRedirect(ILogger logger, string authority, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF callback received OIDC error: {Error} for frontend {FrontendName}")]
    private static partial void LogCallbackError(ILogger logger, string error, string frontendName);

    [LoggerMessage(Level = LogLevel.Error, Message = "BFF callback: token exchange failed for frontend {FrontendName}")]
    private static partial void LogTokenExchangeFailed(ILogger logger, string frontendName);

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF login successful, session {SessionId} created for frontend {FrontendName}")]
    private static partial void LogLoginSuccess(ILogger logger, string sessionId, string frontendName);

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF PAR: pushed authorization request accepted for frontend {FrontendName}")]
    private static partial void LogParSuccess(ILogger logger, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF PAR: request failed with status {StatusCode} for frontend {FrontendName}, falling back to direct parameters")]
    private static partial void LogParRequestFailed(ILogger logger, int statusCode, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF PAR: response missing request_uri for frontend {FrontendName}")]
    private static partial void LogParMissingRequestUri(ILogger logger, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF PAR: falling back to direct parameters for frontend {FrontendName}")]
    private static partial void LogParFallback(ILogger logger, string frontendName);

    [LoggerMessage(Level = LogLevel.Error, Message = "BFF PAR: exception during pushed authorization request for frontend {FrontendName}")]
    private static partial void LogParException(ILogger logger, Exception exception, string frontendName);
}
