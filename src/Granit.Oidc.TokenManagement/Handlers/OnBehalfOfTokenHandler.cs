using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Granit.Caching;
using Granit.MultiTenancy;
using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.ClientAuthentication.Internal;
using Granit.Oidc.DPoP;
using Granit.Oidc.Requests;
using Granit.Oidc.Responses;
using Granit.Oidc.TokenManagement.Diagnostics;
using Granit.Oidc.TokenManagement.Options;
using Granit.Oidc.TokenManagement.Services;
using Granit.Timing;
using Granit.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable GRSEC003 // Handler manages on-behalf-of OAuth 2.0 token exchange including access tokens and DPoP keys

namespace Granit.Oidc.TokenManagement.Handlers;

/// <summary>
/// <see cref="DelegatingHandler"/> that exchanges the inbound caller's access
/// token for an audience-scoped token via OAuth 2.0 Token Exchange (RFC 8693)
/// and attaches it to the outbound request, optionally sender-constrained via
/// DPoP (RFC 9449).
/// </summary>
/// <remarks>
/// <para>
/// Replaces the deleted <c>AuthTokenPropagationHandler</c> which blindly
/// forwarded the caller's bearer token to any host — a classic confused
/// deputy vulnerability. This handler instead:
/// </para>
/// <list type="bullet">
///   <item>Refuses to attach a token when the outbound host is not in
///     <see cref="OnBehalfOfOptions.AllowedHosts"/>.</item>
///   <item>Refuses non-https targets when <see cref="OnBehalfOfOptions.RequireHttps"/>.</item>
///   <item>Swaps the caller's token for one whose <c>aud</c> claim is bound
///     to the downstream audience, limiting blast radius if leaked.</item>
///   <item>Caches exchanged tokens by <c>(tenant, caller_sub, audience,
///     scopes)</c> to avoid round-trips, with fail-open semantics on
///     distributed-cache outage.</item>
///   <item>Binds tokens to a per-process DPoP key by default.</item>
/// </list>
/// </remarks>
[SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "DelegatingHandler injects orthogonal collaborators (token endpoint, cache, DPoP, http context, tenant, user, options, clock, metrics, logger); no natural domain wrapper.")]
internal sealed partial class OnBehalfOfTokenHandler(
    ITokenEndpointService tokenEndpointService,
    IConditionalCache tokenCache,
    IDPoPProofService dpopProofService,
    IHttpContextAccessor httpContextAccessor,
    ICurrentTenant currentTenant,
    ICurrentUserService currentUser,
    IOptionsMonitor<OnBehalfOfOptions> optionsMonitor,
    IClock clock,
    TokenManagementMetrics metrics,
    ILogger<OnBehalfOfTokenHandler> logger) : DelegatingHandler
{
    private const string CacheKeyPrefix = "granit.oidc.obo";

    // Per-handler-instance DPoP key + server-supplied nonce. The handler is a
    // transient delegating handler but HttpClientFactory keeps the outer
    // HttpMessageHandler pinned for the lifetime of its HandlerLifetime, so the
    // key survives across requests for the same named client.
    private string? _dpopPrivateKeyJwk;
    private string? _dpopNonce;
    private readonly Lock _dpopLock = new();

    /// <summary>
    /// The named client configuration to use for resolving
    /// <see cref="OnBehalfOfOptions"/>. Set by the DI extension.
    /// </summary>
    internal string ClientName { get; set; } = string.Empty;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        OnBehalfOfOptions options = optionsMonitor.Get(ClientName);

        // 1. Validate the outbound target. Fail-closed: if the host is not
        //    allow-listed or the scheme is unsafe, the request still goes out
        //    (unauthenticated) rather than carrying a user's token to an
        //    unknown destination.
        if (!IsTargetAllowed(request, options))
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        // 2. Get the inbound caller's token from the ambient HttpContext. Off
        //    the HTTP pipeline (background jobs, hosted services) there is no
        //    user to act on behalf of — send unauthenticated.
        if (!TryExtractInboundToken(out string? subjectToken, out string? subjectSub))
        {
            LogNoHttpContext(ClientName);
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        // 3. Acquire an exchanged token, using the (tenant, sub, aud, scopes)
        //    cache key to avoid round-trips on every call.
        string? exchangedToken = await AcquireExchangedTokenAsync(
            options, subjectToken, subjectSub, cancellationToken).ConfigureAwait(false);

        if (exchangedToken is null)
        {
            // Fail-closed: an IdP refusal or hard failure sends the request
            // unauthenticated so the downstream API returns a clean 401
            // rather than accepting the wrong identity.
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        // 4. Attach the token (+ optional DPoP proof) and send. On 401 with a
        //    nonce challenge, retry once with the fresh nonce.
        ApplyAuthorizationHeader(request, exchangedToken, options.RequireDPoP);

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized
            && options.RequireDPoP
            && TryReadServerNonce(response, out string? freshNonce))
        {
            LogDPoPNonceChallenge(ClientName);
            lock (_dpopLock)
            {
                _dpopNonce = freshNonce;
            }

            response.Dispose();
            using HttpRequestMessage retry = await CloneRequestAsync(request, cancellationToken).ConfigureAwait(false);
            ApplyAuthorizationHeader(retry, exchangedToken, options.RequireDPoP);
            response = await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    // ------------------------------------------------------------------------
    // Target validation
    // ------------------------------------------------------------------------

    private bool IsTargetAllowed(HttpRequestMessage request, OnBehalfOfOptions options)
    {
        if (request.RequestUri is null)
        {
            LogRejectedTarget(ClientName, "null URI");
            return false;
        }

        if (options.RequireHttps
            && !string.Equals(request.RequestUri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            LogRejectedTarget(ClientName, $"non-https scheme '{request.RequestUri.Scheme}'");
            return false;
        }

        string host = request.RequestUri.Host;
        if (options.AllowedHosts.Any(allowed => string.Equals(allowed, host, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        LogRejectedTarget(ClientName, $"host '{host}' not in AllowedHosts");
        return false;
    }

    // ------------------------------------------------------------------------
    // Inbound token extraction
    // ------------------------------------------------------------------------

    private bool TryExtractInboundToken(
        [NotNullWhen(true)] out string? token,
        [NotNullWhen(true)] out string? subject)
    {
        token = null;
        subject = null;

        HttpContext? ctx = httpContextAccessor.HttpContext;
        if (ctx is null)
        {
            return false;
        }

        string? auth = ctx.Request.Headers.Authorization;
        if (string.IsNullOrEmpty(auth))
        {
            return false;
        }

        // Accept "Bearer <token>" or "DPoP <token>" (RFC 9449 §7.1 — the
        // inbound scheme can be either one; the exchange does not care).
        int space = auth.IndexOf(' ', StringComparison.Ordinal);
        if (space <= 0 || space == auth.Length - 1)
        {
            return false;
        }

        string scheme = auth[..space];
        if (!string.Equals(scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(scheme, "DPoP", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = auth[(space + 1)..];

        // Subject identifier for cache partitioning — "anon" falls back when
        // no current user is resolved, which keeps behaviour predictable if
        // the middleware pipeline order ever ends up with anonymous requests
        // reaching a handler that nonetheless carries an Authorization header
        // (unusual but defensible).
        subject = currentUser.UserId ?? "anon";
        return true;
    }

    // ------------------------------------------------------------------------
    // Token acquisition & cache
    // ------------------------------------------------------------------------

    private async Task<string?> AcquireExchangedTokenAsync(
        OnBehalfOfOptions options,
        string subjectToken,
        string subjectSub,
        CancellationToken cancellationToken)
    {
        string tenantSegment = currentTenant.IsAvailable && currentTenant.Id is { } id
            ? id.ToString()
            : "global";

        string scopeValue = options.Scopes.Count > 0 ? string.Join(' ', options.Scopes) : string.Empty;
        string cacheKey = BuildCacheKey(tenantSegment, subjectSub, options.Audience, scopeValue);

        using Activity? activity = TokenManagementActivitySource.Source.StartActivity(
            TokenManagementActivitySource.CacheLookup);
        activity?.SetTag("client_name", ClientName);
        activity?.SetTag("audience", options.Audience);

        // Cache read — fail-open on distributed-cache outage.
        string? cached;
        try
        {
            cached = await tokenCache.GetAsync<string>(cacheKey, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogCacheFailure(ClientName, "GetAsync", ex.Message);
            cached = null;
        }

        if (cached is not null)
        {
            metrics.RecordCacheHit(tenantId: tenantSegment, ClientName);
            return cached;
        }

        metrics.RecordCacheMiss(tenantId: tenantSegment, ClientName);

        // Build and issue the token-exchange request. Hard failure from the
        // IdP is fail-CLOSED on the security boundary (we do NOT fall through
        // to an unauthenticated propagation path).
        TokenExchangeTokenRequest exchange = new()
        {
            ClientId = options.ClientId,
            SubjectToken = subjectToken,
            SubjectTokenType = OidcConstants.TokenTypeIdentifiers.AccessToken,
            Audience = options.Audience,
            Scope = options.Scopes.Count > 0 ? scopeValue : null,
        };

        IClientAuthenticationStrategy? clientAuth = CreateClientAuth(options);
        DPoPOptions? dpop = CreateDPoP(options);

        TokenResponse tokenResponse;
        try
        {
            tokenResponse = await tokenEndpointService
                .RequestTokenAsync(options.Authority, exchange, clientAuth, dpop, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogTokenExchangeException(ClientName, options.Audience, ex.Message);
            return null;
        }

        if (!tokenResponse.IsSuccess)
        {
            LogTokenExchangeFailure(ClientName, options.Audience, tokenResponse.Error.Error);
            return null;
        }

        if (tokenResponse.DPoPNonce is not null)
        {
            lock (_dpopLock)
            {
                _dpopNonce = tokenResponse.DPoPNonce;
            }
        }

        // Write-through cache — fail-open on outage (a subsequent call will
        // simply re-exchange).
        TimeSpan ttl = TimeSpan.FromSeconds(tokenResponse.ExpiresIn) - options.TokenLifetimeSafetyMargin;
        if (ttl > TimeSpan.Zero)
        {
            try
            {
                await tokenCache.SetIfAbsentAsync(cacheKey, tokenResponse.AccessToken, ttl, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogCacheFailure(ClientName, "SetIfAbsentAsync", ex.Message);
            }
        }

        LogTokenExchangeSuccess(ClientName, options.Audience, tokenResponse.ExpiresIn);
        return tokenResponse.AccessToken;
    }

    private static string BuildCacheKey(string tenant, string subjectSub, string audience, string scopes)
    {
        // Hash the variable portion to keep the Redis key short and avoid
        // leaking PII (email-like sub claims) into the key space.
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes($"{subjectSub}|{audience}|{scopes}"), hash);
        return $"{CacheKeyPrefix}:{tenant}:{Convert.ToHexStringLower(hash)}";
    }

    // ------------------------------------------------------------------------
    // Authorization + DPoP application
    // ------------------------------------------------------------------------

    private void ApplyAuthorizationHeader(HttpRequestMessage request, string accessToken, bool useDPoP)
    {
        if (useDPoP)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("DPoP", accessToken);

            string? privateKey;
            string? nonce;
            lock (_dpopLock)
            {
                privateKey = _dpopPrivateKeyJwk;
                nonce = _dpopNonce;
            }

            if (privateKey is not null && request.RequestUri is not null)
            {
                string proof = dpopProofService.CreateProof(
                    privateKey, request.Method.Method, request.RequestUri.ToString(), nonce);
                request.Headers.TryAddWithoutValidation("DPoP", proof);
            }
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }

    private DPoPOptions? CreateDPoP(OnBehalfOfOptions options)
    {
        if (!options.RequireDPoP)
        {
            return null;
        }

        lock (_dpopLock)
        {
            _dpopPrivateKeyJwk ??= dpopProofService.GenerateKeyPair();
            return new DPoPOptions(_dpopPrivateKeyJwk, _dpopNonce);
        }
    }

    private IClientAuthenticationStrategy? CreateClientAuth(OnBehalfOfOptions options) =>
        options.ClientAuthenticationMethod switch
        {
            ClientAuthenticationMethod.ClientSecretPost when options.ClientSecret is not null
                => new ClientSecretPostStrategy(options.ClientSecret),
            ClientAuthenticationMethod.PrivateKeyJwt when options.ClientSigningKeyJwk is not null
                => new PrivateKeyJwtStrategy(options.ClientSigningKeyJwk, clock),
            _ => null,
        };

    private static bool TryReadServerNonce(HttpResponseMessage response, out string? nonce)
    {
        nonce = null;
        if (response.Headers.TryGetValues("DPoP-Nonce", out IEnumerable<string>? values))
        {
            nonce = values.FirstOrDefault();
            return nonce is not null;
        }

        return false;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage original, CancellationToken cancellationToken)
    {
        HttpRequestMessage clone = new(original.Method, original.RequestUri) { Version = original.Version };

        foreach (KeyValuePair<string, IEnumerable<string>> h in original.Headers
            .Where(h => !string.Equals(h.Key, "Authorization", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(h.Key, "DPoP", StringComparison.OrdinalIgnoreCase)))
        {
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }

        if (original.Content is not null)
        {
            byte[] body = await original.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            clone.Content = new ByteArrayContent(body);
            if (original.Content.Headers.ContentType is not null)
            {
                clone.Content.Headers.ContentType = original.Content.Headers.ContentType;
            }
        }

        return clone;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "OBO[{ClientName}]: rejected token attachment — {Reason}. Request sent unauthenticated.")]
    private partial void LogRejectedTarget(string clientName, string reason);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "OBO[{ClientName}]: no ambient HttpContext (background job?) — request sent unauthenticated.")]
    private partial void LogNoHttpContext(string clientName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "OBO[{ClientName}]: cache operation {Operation} failed — falling back to IdP round-trip. {ErrorMessage}")]
    private partial void LogCacheFailure(string clientName, string operation, string errorMessage);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "OBO[{ClientName}]: token exchange for audience {Audience} failed — {Error}. Request sent unauthenticated.")]
    private partial void LogTokenExchangeFailure(string clientName, string audience, string error);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "OBO[{ClientName}]: token exchange for audience {Audience} threw — {ErrorMessage}. Request sent unauthenticated.")]
    private partial void LogTokenExchangeException(string clientName, string audience, string errorMessage);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "OBO[{ClientName}]: token exchange for audience {Audience} succeeded (expires_in={ExpiresIn}s).")]
    private partial void LogTokenExchangeSuccess(string clientName, string audience, int expiresIn);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "OBO[{ClientName}]: server returned 401 with DPoP-Nonce — retrying once with fresh nonce.")]
    private partial void LogDPoPNonceChallenge(string clientName);
}

#pragma warning restore GRSEC003
