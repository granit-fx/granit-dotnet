using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Bff.Diagnostics;
using Granit.Bff.Endpoints.Internal;
using Granit.Bff.Options;
using Granit.Events;
using Granit.Http.Cookies;
using Granit.Identity;
using Granit.MultiTenancy;
using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.ClientAuthentication.Internal;
using Granit.Oidc.DPoP;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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
    private const string LoggerCategory = "Granit.Bff.Endpoints.BffLoginEndpoints";

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
                string? code,
                string? state,
                string? error,
                string? iss,
                CancellationToken cancellationToken) =>
                HandleCallbackAsync(httpContext, frontend, code, state, error, iss, cancellationToken))
            .WithName($"BffCallback_{frontend.Name}")
            .WithSummary("Handles the OIDC callback, exchanges the code for tokens, and sets the session cookie.")
            .WithDescription(
                "Receives the authorization code from the OIDC provider, exchanges it for tokens "
                + "using the stored PKCE code verifier, stores the tokens server-side, sets a session "
                + "cookie, and redirects to the configured post-login path. On error, redirects to "
                + "the frontend error page with an error code query parameter.")
            .Produces(StatusCodes.Status302Found)
            .ExcludeFromDescription();

        return group;
    }

    private static async Task<RedirectHttpResult> HandleLoginAsync(
        HttpContext httpContext,
        BffFrontendOptions frontend,
        CancellationToken cancellationToken)
    {
        IServiceProvider services = httpContext.RequestServices;
        GranitBffOptions bffOptions = services.GetRequiredService<IOptions<GranitBffOptions>>().Value;
        IFusionCache cache = services.GetRequiredService<IFusionCache>();
        IDPoPProofService dpopService = services.GetRequiredService<IDPoPProofService>();
        ILogger logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(LoggerCategory);

        Activity? activity = BffActivitySource.Source.StartActivity(BffActivitySource.Login);
        using IDisposable? activityScope = activity;

        // Generate PKCE code verifier and challenge
        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = ComputeCodeChallenge(codeVerifier);

        // Generate state parameter to correlate callback
        string state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        // Generate DPoP key pair if enabled (stored alongside PKCE state, used during token exchange)
        string? dpopPrivateKeyJwk = frontend.UseDPoP ? dpopService.GenerateKeyPair() : null;

        // Read and validate the caller-supplied returnUrl (open-redirect prevention)
        string? returnUrl = ValidateReturnUrl(httpContext.Request.Query["returnUrl"], frontend);

        // Store code_verifier + state + frontend name + DPoP key + returnUrl in cache (short TTL for the auth round-trip)
        PkceState pkceData = new(codeVerifier, state, frontend.Name, dpopPrivateKeyJwk, returnUrl);

        await cache.SetAsync(
            $"{PkceKeyPrefix}{state}",
            pkceData,
            new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(10) },
            token: cancellationToken).ConfigureAwait(false);

        // Build authorization URL.
        // Deduplicate defensively: IConfiguration binding onto a pre-initialised
        // string[] can produce duplicates when the default values and an
        // appsettings section declare overlapping scopes. Duplicates in the
        // `scope` parameter are tolerated by most OIDC servers but bloat the
        // /connect/authorize URL and were observed in production on 2026-04-22.
#pragma warning disable GRSEC003 // Building OIDC authorize URL with client credentials
        string scopes = string.Join(" ", frontend.Scopes.Distinct(StringComparer.Ordinal));
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
                return RedirectToError(frontend, "par_failed");
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

    // Validates the authorization-response issuer (RFC 9207). Returns an error redirect to send
    // back, or null when the issuer is present and matches the configured authority.
    // Reads the device-trust binding stashed by the device-trust middleware, returning the device id only when
    // it is bound to the same user the session belongs to. Null (untrusted) when the middleware did not run or no
    // valid device cookie was presented.
    private static string? ResolveTrustedDeviceId(HttpContext httpContext, string userId) =>
        httpContext.Items.TryGetValue(DeviceTrustContextItems.UserId, out object? boundUser)
        && boundUser is string boundUserId
        && string.Equals(boundUserId, userId, StringComparison.Ordinal)
        && httpContext.Items.TryGetValue(DeviceTrustContextItems.DeviceId, out object? deviceId)
            ? deviceId as string
            : null;

    private static async Task<RedirectHttpResult?> ValidateCallbackIssuerAsync(
        HttpContext httpContext, GranitBffOptions bffOptions, BffFrontendOptions frontend,
        string? iss, CancellationToken cancellationToken)
    {
        ILogger logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(LoggerCategory);
        string expectedIssuer = bffOptions.Authority.ToString().TrimEnd('/');

        if (string.IsNullOrEmpty(iss))
        {
            LogIssuerMissing(logger, frontend.Name);
            await TryWriteAuthAuditAsync(httpContext, logger, userId: null, userName: null,
                failureReason: "issuer_missing", cancellationToken).ConfigureAwait(false);
            return RedirectToError(frontend, "issuer_missing");
        }

        if (!string.Equals(iss.TrimEnd('/'), expectedIssuer, StringComparison.OrdinalIgnoreCase))
        {
            LogIssuerMismatch(logger, iss, expectedIssuer, frontend.Name);
            await TryWriteAuthAuditAsync(httpContext, logger, userId: null, userName: null,
                failureReason: "issuer_mismatch", cancellationToken).ConfigureAwait(false);
            return RedirectToError(frontend, "issuer_mismatch");
        }

        return null;
    }

    private static async Task<RedirectHttpResult> HandleCallbackAsync(
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
            .CreateLogger(LoggerCategory);

        Activity? activity = BffActivitySource.Source.StartActivity(BffActivitySource.Callback);
        using IDisposable? activityScope = activity;

        RedirectHttpResult? parameterError = await ValidateCallbackParametersAsync(
            httpContext, frontend, code, state, error, cancellationToken).ConfigureAwait(false);
        if (parameterError is not null)
        {
            return parameterError;
        }

        // Verify authorization response issuer (RFC 9207, FAPI 2.0 §5.3.3.2)
        if (bffOptions.RequireIssuerValidation)
        {
            RedirectHttpResult? issuerError = await ValidateCallbackIssuerAsync(
                httpContext, bffOptions, frontend, iss, cancellationToken).ConfigureAwait(false);
            if (issuerError is not null)
            {
                return issuerError;
            }
        }

        // Retrieve and remove PKCE state
        string pkceKey = $"{PkceKeyPrefix}{state}";
        MaybeValue<PkceState> maybePkce = await cache.TryGetAsync<PkceState>(pkceKey, token: cancellationToken)
            .ConfigureAwait(false);

        if (!maybePkce.HasValue)
        {
            await TryWriteAuthAuditAsync(httpContext, logger, userId: null, userName: null,
                failureReason: "invalid_state", cancellationToken).ConfigureAwait(false);
            return RedirectToError(frontend, "invalid_state");
        }

        await cache.RemoveAsync(pkceKey, token: cancellationToken).ConfigureAwait(false);

        PkceState pkceState = maybePkce.Value;

        // Exchange authorization code for tokens
        string pathPrefix = string.IsNullOrEmpty(frontend.PathPrefix) ? "" : frontend.PathPrefix;
        string callbackUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{pathPrefix}/bff/callback";
        BffTokenSet? tokens = await ExchangeCodeForTokensAsync(
            services, frontend, code!, pkceState.CodeVerifier, callbackUrl,
            pkceState.DPoPPrivateKeyJwk, cancellationToken)
            .ConfigureAwait(false);

        if (tokens is null)
        {
            LogTokenExchangeFailed(logger, frontend.Name);
            await TryWriteAuthAuditAsync(httpContext, logger, userId: null, userName: null,
                failureReason: "token_exchange_failed", cancellationToken).ConfigureAwait(false);
            return RedirectToError(frontend, "token_exchange_failed");
        }

        // Enrich token set with user context for session management (#621)
        string? userId = ExtractSubFromIdToken(tokens.IdToken);
        string? userAgent = httpContext.Request.Headers.UserAgent.ToString();
        tokens = tokens with
        {
            UserId = userId,
            UserAgent = string.IsNullOrEmpty(userAgent) ? null : userAgent,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
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
        LogLoginSuccess(logger, BffSessionIdMasking.Mask(sessionId), frontend.Name);

        // Announce the new session out-of-band so consumers (anomaly detection, geo enrichment, notifications)
        // react without the login path waiting on them. Best-effort: a no-op when no distributed bus is wired.
        if (userId is not null && services.GetService<IDistributedEventBus>() is { } eventBus)
        {
            Guid? tenantId = services.GetService<ICurrentTenant>() is { IsAvailable: true } tenant ? tenant.Id : null;
            await eventBus.PublishAsync(
                new UserSessionCreatedEto(
                    userId,
                    sessionId,
                    tenantId,
                    UserSessionSource.Bff,
                    tokens.UserAgent,
                    tokens.IpAddress,
                    tokens.SessionCreatedAt,
                    ResolveTrustedDeviceId(httpContext, userId)),
                cancellationToken).ConfigureAwait(false);
        }

        // The BFF owns the authentication audit trail for interactive logins it fronts: it carries the
        // real browser User-Agent and the authenticated user as CreatedBy. The authorization server's
        // token endpoint deliberately does NOT audit the authorization_code / refresh_token exchange
        // (see ConnectTokenEndpoints), so this is the single success row for a BFF login.
        await TryWriteAuthAuditAsync(httpContext, logger,
            userId: string.IsNullOrEmpty(userId) ? AuthenticationAuditEntry.UnknownUserSentinel : userId,
            userName: ExtractClaimFromIdToken(tokens.IdToken, "preferred_username")
                ?? ExtractClaimFromIdToken(tokens.IdToken, "email"),
            failureReason: null,
            cancellationToken).ConfigureAwait(false);

        // Redirect to the original URL the user requested, or fall back to the configured post-login path
        string redirectUrl = !string.IsNullOrEmpty(pkceState.ReturnUrl)
            ? frontend.PrefixWithClientUrl(pkceState.ReturnUrl)
            : frontend.EffectivePostLoginRedirectPath;

        return TypedResults.Redirect(redirectUrl);
    }

    // Rejects malformed callback parameters before any state is consumed: a provider-reported error, or a
    // missing authorization code / state. Returns an error redirect (audited) or null when the basics are sound.
    private static async Task<RedirectHttpResult?> ValidateCallbackParametersAsync(
        HttpContext httpContext,
        BffFrontendOptions frontend,
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken)
    {
        ILogger logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(LoggerCategory);

        if (!string.IsNullOrEmpty(error))
        {
            string safeError = KnownOidcErrors.Contains(error) ? error : "unknown_error";
            LogCallbackError(logger, safeError, frontend.Name);
            await TryWriteAuthAuditAsync(httpContext, logger, userId: null, userName: null,
                failureReason: safeError, cancellationToken).ConfigureAwait(false);
            return RedirectToError(frontend, safeError);
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            await TryWriteAuthAuditAsync(httpContext, logger, userId: null, userName: null,
                failureReason: "missing_code_or_state", cancellationToken).ConfigureAwait(false);
            return RedirectToError(frontend, "missing_code_or_state");
        }

        return null;
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

    internal static string BuildDirectAuthorizeUrl(
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
            .CreateLogger(LoggerCategory);
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

    internal static string GenerateCodeVerifier()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(CodeVerifierLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    internal static string ComputeCodeChallenge(string codeVerifier)
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
    internal static string? ExtractSubFromIdToken(string? idToken) =>
        ExtractClaimFromIdToken(idToken, "sub");

    /// <summary>
    /// Extracts a single string claim from an ID token JWT payload without full
    /// validation (token was already validated by the authorization server during
    /// exchange). Returns <see langword="null"/> when the token is malformed or
    /// the claim is absent.
    /// </summary>
    internal static string? ExtractClaimFromIdToken(string? idToken, string claimName)
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
            return doc.RootElement.TryGetProperty(claimName, out JsonElement claim)
                ? claim.GetString() : null;
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

    /// <summary>
    /// Validates a caller-supplied <c>returnUrl</c> to prevent open-redirect attacks.
    /// Only relative paths (starting with <c>/</c>) or URLs matching the frontend's
    /// <see cref="BffFrontendOptions.ClientUrl"/> origin are accepted.
    /// Returns the validated relative path, or <see langword="null"/> if the URL is invalid.
    /// </summary>
    internal static string? ValidateReturnUrl(string? returnUrl, BffFrontendOptions frontend)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        // Must be a relative path starting with "/", reject protocol-relative URLs (//evil.com)
        if (!returnUrl.StartsWith('/') || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        // Reject URLs containing a scheme (e.g. javascript:, data:, or http: after path traversal)
        if (returnUrl.Contains(':', StringComparison.Ordinal))
        {
            return null;
        }

        return returnUrl;
    }

    internal sealed record PkceState(string CodeVerifier, string State, string FrontendName, string? DPoPPrivateKeyJwk = null, string? ReturnUrl = null);

    /// <summary>
    /// Builds a redirect to the frontend error page with the given error code as a query parameter.
    /// </summary>
    private static RedirectHttpResult RedirectToError(BffFrontendOptions frontend, string errorCode) =>
        TypedResults.Redirect($"{frontend.EffectiveErrorRedirectPath}?error={Uri.EscapeDataString(errorCode)}");

    private static IClientAuthenticationStrategy ResolveClientAuth(BffFrontendOptions frontend, IClock clock) =>
        frontend.ClientAuthenticationMethod == BffClientAuthenticationMethod.PrivateKeyJwt
            ? new PrivateKeyJwtStrategy(frontend.ClientSigningKeyJwk!, clock)
            : new ClientSecretPostStrategy(frontend.ClientSecret);

    /// <summary>
    /// Records an authentication audit row for the BFF callback. No-op when
    /// <see cref="IAuditingWriter"/> is not registered (apps that don't opt into
    /// auditing persistence). Audit failures are swallowed so a transient
    /// audit-store outage never breaks the login flow.
    /// </summary>
    private static async Task TryWriteAuthAuditAsync(
        HttpContext httpContext,
        ILogger logger,
        string? userId,
        string? userName,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        IAuditingWriter? auditingWriter = httpContext.RequestServices.GetService<IAuditingWriter>();
        if (auditingWriter is null)
        {
            return;
        }

        TimeProvider timeProvider = httpContext.RequestServices.GetService<TimeProvider>()
            ?? TimeProvider.System;
        ICurrentTenant? currentTenant = httpContext.RequestServices.GetService<ICurrentTenant>();
        Guid? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id : null;
        string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        string? userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrEmpty(userAgent))
        {
            userAgent = null;
        }
        string? correlationId = Activity.Current?.Id;

        AuditEntry entry = failureReason is null
            ? AuthenticationAuditEntry.CreateSuccess(
                timeProvider.GetUtcNow(), userId!, userName, method: "bff_session",
                tenantId, ipAddress, userAgent, correlationId)
            : AuthenticationAuditEntry.CreateFailure(
                timeProvider.GetUtcNow(), userId, userName, method: "bff_session", failureReason,
                tenantId, ipAddress, userAgent, correlationId);

        try
        {
            await auditingWriter.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAuditWriteFailed(logger, ex);
        }
    }

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

    [LoggerMessage(Level = LogLevel.Error, Message = "BFF callback: failed to write authentication audit entry — login flow continues.")]
    private static partial void LogAuditWriteFailed(ILogger logger, Exception exception);
}
