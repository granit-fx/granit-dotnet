using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Bff.Diagnostics;
using Granit.Bff.Options;
using Granit.Http.Cookies;
using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.ClientAuthentication.Internal;
using Granit.Oidc.DPoP;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Bff.Endpoints.Endpoints;

/// <summary>
/// BFF login and callback endpoints. Implements OIDC authorization code flow with PKCE.
/// Registered per-frontend under <c>/{pathPrefix}/bff/login</c>.
/// </summary>
internal static partial class BffLoginEndpoints
{
    private const string PkceKeyPrefix = "bff:pkce:";
    private const int CodeVerifierLength = 64;
    private const int SessionIdByteLength = 32;

    /// <summary>
    /// Known OIDC error codes (RFC 6749 §4.1.2.1, §5.2 + OIDC Core §3.1.2.6).
    /// Unknown codes are replaced with <c>"unknown_error"</c> to prevent reflection.
    /// </summary>
    private static readonly HashSet<string> KnownOidcErrors = new(StringComparer.Ordinal)
    {
        "invalid_request", "unauthorized_client", "access_denied",
        "unsupported_response_type", "invalid_scope", "server_error",
        "temporarily_unavailable", "invalid_grant", "invalid_client",
        "interaction_required", "login_required", "account_selection_required",
        "consent_required", "invalid_request_uri", "invalid_request_object",
        "request_not_supported", "request_uri_not_supported",
        "registration_not_supported",
    };

    internal static RouteGroupBuilder MapLoginEndpoints(this RouteGroupBuilder group, BffFrontendOptions frontend)
    {
        group.MapGet("/login", (HttpContext httpContext, CancellationToken cancellationToken) =>
                HandleLoginAsync(httpContext, frontend, cancellationToken))
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
                [FromQuery] string? iss,
                CancellationToken cancellationToken) =>
                HandleCallbackAsync(httpContext, frontend, code, state, error, iss, cancellationToken))
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

    private static async Task<Results<RedirectHttpResult, ProblemHttpResult>> HandleLoginAsync(
        HttpContext httpContext,
        BffFrontendOptions frontend,
        CancellationToken cancellationToken)
    {
        IServiceProvider services = httpContext.RequestServices;
        GranitBffOptions bffOptions = services.GetRequiredService<IOptions<GranitBffOptions>>().Value;
        IFusionCache cache = services.GetRequiredService<IFusionCache>();
        IDPoPProofService dpopService = services.GetRequiredService<IDPoPProofService>();
        ILogger logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.Bff.Endpoints.BffLoginEndpoints");

        Activity? activity = BffActivitySource.Source.StartActivity(BffActivitySource.Login);
        using IDisposable? activityScope = activity;

        // Generate PKCE code verifier and challenge
        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = ComputeCodeChallenge(codeVerifier);

        // Generate state parameter to correlate callback
        string state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        // Generate DPoP key pair if enabled (stored alongside PKCE state, used during token exchange)
        string? dpopPrivateKeyJwk = frontend.UseDPoP ? dpopService.GenerateKeyPair() : null;

        // Store code_verifier + state + frontend name + DPoP key in cache (short TTL for the auth round-trip)
        PkceState pkceData = new(codeVerifier, state, frontend.Name, dpopPrivateKeyJwk);

        await cache.SetAsync(
            $"{PkceKeyPrefix}{state}",
            pkceData,
            new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(10) },
            token: cancellationToken).ConfigureAwait(false);

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
                services, frontend, callbackUrl, scopes, state,
                codeChallenge, cancellationToken).ConfigureAwait(false);

            if (requestUri is not null)
            {
                authorizeUrl = $"{authorityBase}/connect/authorize"
                    + $"?client_id={Uri.EscapeDataString(frontend.ClientId)}"
                    + $"&request_uri={Uri.EscapeDataString(requestUri)}";
            }
            else if (frontend.RequirePushedAuthorizationRequests)
            {
                // PAR is mandatory (FAPI 2.0 strict) — do not fall back
                LogParFallback(logger, frontend.Name);
                return TypedResults.Problem(
                    detail: "Pushed Authorization Request failed and is required by configuration.",
                    statusCode: StatusCodes.Status502BadGateway);
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
        string? iss,
        CancellationToken cancellationToken)
    {
        IServiceProvider services = httpContext.RequestServices;
        GranitBffOptions bffOptions = services.GetRequiredService<IOptions<GranitBffOptions>>().Value;
        IFusionCache cache = services.GetRequiredService<IFusionCache>();
        IBffTokenStore tokenStore = services.GetRequiredService<IBffTokenStore>();
        BffMetrics metrics = services.GetRequiredService<BffMetrics>();
        ILogger logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.Bff.Endpoints.BffLoginEndpoints");

        Activity? activity = BffActivitySource.Source.StartActivity(BffActivitySource.Callback);
        using IDisposable? activityScope = activity;

        if (!string.IsNullOrEmpty(error))
        {
            string safeError = KnownOidcErrors.Contains(error) ? error : "unknown_error";
            LogCallbackError(logger, safeError, frontend.Name);
            return TypedResults.Problem(
                detail: $"OIDC authorization error: {safeError}",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return TypedResults.Problem(
                detail: "Missing authorization code or state parameter.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Verify authorization response issuer (RFC 9207, FAPI 2.0 §5.3.3.2)
        if (bffOptions.RequireIssuerValidation)
        {
            string expectedIssuer = bffOptions.Authority.ToString().TrimEnd('/');

            if (string.IsNullOrEmpty(iss))
            {
                LogIssuerMissing(logger, frontend.Name);
                return TypedResults.Problem(
                    detail: "Missing iss parameter in authorization response (RFC 9207).",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (!string.Equals(iss.TrimEnd('/'), expectedIssuer, StringComparison.OrdinalIgnoreCase))
            {
                LogIssuerMismatch(logger, iss, expectedIssuer, frontend.Name);
                return TypedResults.Problem(
                    detail: "Authorization response issuer does not match expected authority.",
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }

        // Retrieve and remove PKCE state
        string pkceKey = $"{PkceKeyPrefix}{state}";
        MaybeValue<PkceState> maybePkce = await cache.TryGetAsync<PkceState>(pkceKey, token: cancellationToken)
            .ConfigureAwait(false);

        if (!maybePkce.HasValue)
        {
            return TypedResults.Problem(
                detail: "Invalid or expired state parameter.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await cache.RemoveAsync(pkceKey, token: cancellationToken).ConfigureAwait(false);

        PkceState pkceState = maybePkce.Value;

        // Exchange authorization code for tokens
        string pathPrefix = string.IsNullOrEmpty(frontend.PathPrefix) ? "" : frontend.PathPrefix;
        string callbackUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{pathPrefix}/bff/callback";
        BffTokenSet? tokens = await ExchangeCodeForTokensAsync(
            services, frontend, code, pkceState.CodeVerifier, callbackUrl,
            pkceState.DPoPPrivateKeyJwk, cancellationToken)
            .ConfigureAwait(false);

        if (tokens is null)
        {
            LogTokenExchangeFailed(logger, frontend.Name);
            return TypedResults.Problem(
                detail: "Failed to exchange authorization code for tokens.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Enrich token set with user context for session management (#621)
        string? userId = ExtractSubFromIdToken(tokens.IdToken);
        string? userAgent = httpContext.Request.Headers.UserAgent.ToString();
        tokens = tokens with
        {
            UserId = userId,
            UserAgent = string.IsNullOrEmpty(userAgent) ? null : userAgent,
        };

        // Generate session ID with 256 bits of cryptographic entropy (OWASP ASVS 3.2.2)
#pragma warning disable GRSEC002 // Session IDs are ephemeral cache keys, not clustered index values
        string sessionId = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SessionIdByteLength))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
#pragma warning restore GRSEC002
        await tokenStore.StoreAsync(frontend.Name, sessionId, tokens, cancellationToken).ConfigureAwait(false);

        // Set frontend-specific session cookie via managed cookie system (GRSEC004)
        IGranitCookieManager cookieManager = services.GetRequiredService<IGranitCookieManager>();
        await cookieManager.SetCookieAsync(httpContext, frontend.SessionCookieName, sessionId)
            .ConfigureAwait(false);

        metrics.RecordLogin(null);
        LogLoginSuccess(logger, MaskSessionId(sessionId), frontend.Name);

        return TypedResults.Redirect(frontend.EffectivePostLoginRedirectPath);
    }

#pragma warning disable GRSEC003 // Method handles tokens — server-side only
    private static async Task<BffTokenSet?> ExchangeCodeForTokensAsync(
        IServiceProvider services,
        BffFrontendOptions frontend,
        string code,
        string codeVerifier,
        string redirectUri,
        string? dpopPrivateKeyJwk,
        CancellationToken cancellationToken)
    {
        IHttpClientFactory httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
        IDPoPProofService dpopService = services.GetRequiredService<IDPoPProofService>();
        IClock clock = services.GetRequiredService<IClock>();
        GranitBffOptions options = services.GetRequiredService<IOptions<GranitBffOptions>>().Value;

        using HttpClient httpClient = httpClientFactory.CreateClient("Granit.Bff");
        string tokenEndpoint = $"{options.Authority.ToString().TrimEnd('/')}/connect/token";

        Dictionary<string, string> parameters = new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = frontend.ClientId,
            ["code_verifier"] = codeVerifier,
        };

        ResolveClientAuth(frontend, clock).Apply(parameters, frontend.ClientId, tokenEndpoint);

        HttpResponseMessage response = await SendTokenRequestAsync(
            httpClient, tokenEndpoint, parameters, dpopPrivateKeyJwk, null, dpopService, cancellationToken)
            .ConfigureAwait(false);

        // Handle use_dpop_nonce error — single retry with server-provided nonce (RFC 9449 §8)
        string? dpopNonce = null;
        if (!response.IsSuccessStatusCode
            && !string.IsNullOrEmpty(dpopPrivateKeyJwk)
            && await IsDPoPNonceRequiredAsync(response, cancellationToken).ConfigureAwait(false))
        {
            dpopNonce = response.Headers.TryGetValues("DPoP-Nonce", out IEnumerable<string>? nonceValues)
                ? nonceValues.FirstOrDefault() : null;

            if (!string.IsNullOrEmpty(dpopNonce))
            {
                response.Dispose();
                response = await SendTokenRequestAsync(
                    httpClient, tokenEndpoint, parameters, dpopPrivateKeyJwk, dpopNonce, dpopService, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            // Capture DPoP-Nonce from success response for future requests
            if (response.Headers.TryGetValues("DPoP-Nonce", out IEnumerable<string>? successNonce))
            {
                dpopNonce = successNonce.FirstOrDefault() ?? dpopNonce;
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
                SessionCreatedAt = clock.Now,
                DPoPPrivateKeyJwk = dpopPrivateKeyJwk,
                DPoPNonce = dpopNonce,
            };
        }
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
        IServiceProvider services,
        BffFrontendOptions frontend,
        string callbackUrl,
        string scopes,
        string state,
        string codeChallenge,
        CancellationToken cancellationToken)
    {
        IHttpClientFactory httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
        IClock clock = services.GetRequiredService<IClock>();
        ILogger logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.Bff.Endpoints.BffLoginEndpoints");
        GranitBffOptions bffOptions = services.GetRequiredService<IOptions<GranitBffOptions>>().Value;
        string authorityBase = bffOptions.Authority.ToString().TrimEnd('/');

        try
        {
            using HttpClient httpClient = httpClientFactory.CreateClient("Granit.Bff");
            string parEndpoint = $"{authorityBase}/connect/par";

            Dictionary<string, string> parameters = new()
            {
                ["client_id"] = frontend.ClientId,
                ["response_type"] = "code",
                ["redirect_uri"] = callbackUrl,
                ["scope"] = scopes,
                ["state"] = state,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256",
            };

            ResolveClientAuth(frontend, clock).Apply(parameters, frontend.ClientId, parEndpoint);

            using FormUrlEncodedContent content = new(parameters);
            using HttpResponseMessage response = await httpClient
                .PostAsync(parEndpoint, content, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                LogParRequestFailed(logger, (int)response.StatusCode, frontend.Name);
                return null;
            }

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            JsonElement parResponse = await JsonSerializer.DeserializeAsync<JsonElement>(
                stream, cancellationToken: cancellationToken)
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

    private static async Task<HttpResponseMessage> SendTokenRequestAsync(
        HttpClient httpClient,
        string tokenEndpoint,
        Dictionary<string, string> parameters,
        string? dpopPrivateKeyJwk,
        string? dpopNonce,
        IDPoPProofService dpopService,
        CancellationToken cancellationToken)
    {
        using FormUrlEncodedContent content = new(parameters);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint) { Content = content };

        if (!string.IsNullOrEmpty(dpopPrivateKeyJwk))
        {
            string dpopProof = dpopService.CreateProof(dpopPrivateKeyJwk, "POST", tokenEndpoint, dpopNonce);
            request.Headers.TryAddWithoutValidation("DPoP", dpopProof);
        }

        return await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> IsDPoPNonceRequiredAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using Stream errorStream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            JsonElement errorBody = await JsonSerializer.DeserializeAsync<JsonElement>(
                errorStream, cancellationToken: cancellationToken).ConfigureAwait(false);

            return errorBody.TryGetProperty("error", out JsonElement errorCode)
                && errorCode.GetString() == "use_dpop_nonce";
        }
        catch (JsonException)
        {
            return false;
        }
    }

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

    /// <summary>
    /// Extracts the <c>sub</c> claim from an ID token JWT payload without full validation
    /// (token was already validated by the authorization server during exchange).
    /// </summary>
    private static string? ExtractSubFromIdToken(string? idToken)
    {
        if (string.IsNullOrEmpty(idToken))
        {
            return null;
        }

        try
        {
            string[] parts = idToken.Split('.');
            if (parts.Length < 2)
            {
                return null;
            }

            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            byte[] bytes = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(bytes);
            return doc.RootElement.TryGetProperty("sub", out JsonElement sub)
                ? sub.GetString() : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string MaskSessionId(string sessionId) =>
        sessionId.Length > 8 ? $"{sessionId[..4]}...{sessionId[^4..]}" : "****";

    private sealed record PkceState(string CodeVerifier, string State, string FrontendName, string? DPoPPrivateKeyJwk = null);

    private static IClientAuthenticationStrategy ResolveClientAuth(BffFrontendOptions frontend, IClock clock) =>
        frontend.ClientAuthenticationMethod == BffClientAuthenticationMethod.PrivateKeyJwt
            ? new PrivateKeyJwtStrategy(frontend.ClientSigningKeyJwk!, clock)
            : new ClientSecretPostStrategy(frontend.ClientSecret);

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF callback: missing iss parameter in authorization response for frontend {FrontendName} (RFC 9207)")]
    private static partial void LogIssuerMissing(ILogger logger, string frontendName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BFF callback: issuer mismatch — received '{ReceivedIssuer}', expected '{ExpectedIssuer}' for frontend {FrontendName}")]
    private static partial void LogIssuerMismatch(ILogger logger, string receivedIssuer, string expectedIssuer, string frontendName);
}
