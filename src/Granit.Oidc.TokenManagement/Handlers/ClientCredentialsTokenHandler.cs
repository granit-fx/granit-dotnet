using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Granit.Oidc.ClientAuthentication;
using Granit.Oidc.ClientAuthentication.Internal;
using Granit.Oidc.DPoP;
using Granit.Oidc.Requests;
using Granit.Oidc.Responses;
using Granit.Oidc.TokenManagement.Cache;
using Granit.Oidc.TokenManagement.Diagnostics;
using Granit.Oidc.TokenManagement.Options;
using Granit.Oidc.TokenManagement.Services;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable GRSEC003 // Handler manages access tokens and DPoP keys for outbound HTTP requests

namespace Granit.Oidc.TokenManagement.Handlers;

/// <summary>
/// A <see cref="DelegatingHandler"/> that automatically acquires, caches, and attaches
/// client credentials tokens to outbound HTTP requests. Handles 401 retry with token refresh
/// and supports DPoP sender-constrained tokens.
/// </summary>
internal sealed partial class ClientCredentialsTokenHandler(
    ITokenEndpointService tokenEndpointService,
    IClientCredentialsTokenCache tokenCache,
    IDPoPProofService dpopProofService,
    IOptionsMonitor<ClientCredentialsOptions> clientCredentialsOptionsMonitor,
    IOptions<TokenManagementOptions> tokenManagementOptions,
    IClock clock,
    TokenManagementMetrics metrics,
    ILogger<ClientCredentialsTokenHandler> logger) : DelegatingHandler
{
    private string? _dpopPrivateKeyJwk;
    private string? _dpopNonce;
    private readonly Lock _dpopLock = new();

    /// <summary>
    /// The named client configuration to use for resolving <see cref="ClientCredentialsOptions"/>.
    /// Set by the DI extension during handler registration.
    /// </summary>
    internal string ClientName { get; set; } = string.Empty;

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ClientCredentialsOptions options = clientCredentialsOptionsMonitor.Get(ClientName);

        string? accessToken = await AcquireTokenAsync(options, cancellationToken).ConfigureAwait(false);

        if (accessToken is not null)
        {
            ApplyAuthorizationHeader(request, accessToken, options.UseDPoP);
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // On 401 Unauthorized, invalidate the cached token and retry once
        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            LogUnauthorizedRetry(ClientName, request.RequestUri?.ToString() ?? "unknown");

            await tokenCache.RemoveTokenAsync(ClientName, cancellationToken).ConfigureAwait(false);
            accessToken = await AcquireTokenAsync(options, cancellationToken).ConfigureAwait(false);

            if (accessToken is not null)
            {
                // Dispose the original failed response before retrying
                response.Dispose();

                using HttpRequestMessage retryRequest = await CloneRequestAsync(request, cancellationToken).ConfigureAwait(false);
                ApplyAuthorizationHeader(retryRequest, accessToken, options.UseDPoP);

                response = await base.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
            }
        }

        return response;
    }

    private async Task<string?> AcquireTokenAsync(
        ClientCredentialsOptions options,
        CancellationToken cancellationToken)
    {
        using Activity? activity = TokenManagementActivitySource.Source.StartActivity(
            TokenManagementActivitySource.CacheLookup);
        activity?.SetTag("client_name", ClientName);

        string? cachedToken = await tokenCache
            .GetTokenAsync(ClientName, cancellationToken)
            .ConfigureAwait(false);

        if (cachedToken is not null)
        {
            metrics.RecordCacheHit(tenantId: null, ClientName);
            return cachedToken;
        }

        metrics.RecordCacheMiss(tenantId: null, ClientName);

        ClientCredentialsTokenRequest tokenRequest = new()
        {
            ClientId = options.ClientId,
            Scope = options.Scope,
        };

        IClientAuthenticationStrategy? clientAuth = CreateClientAuthStrategy(options);
        DPoPOptions? dpop = CreateDPoPOptions(options);

        TokenResponse tokenResponse = await tokenEndpointService
            .RequestTokenAsync(options.Authority, tokenRequest, clientAuth, dpop, cancellationToken)
            .ConfigureAwait(false);

        if (!tokenResponse.IsSuccess)
        {
            LogTokenAcquisitionFailure(ClientName, tokenResponse.Error.Error, tokenResponse.Error.ErrorDescription);
            return null;
        }

        // Store the DPoP nonce for future proof generation
        if (tokenResponse.DPoPNonce is not null)
        {
            lock (_dpopLock)
            {
                _dpopNonce = tokenResponse.DPoPNonce;
            }
        }

        TimeSpan margin = options.CacheMargin ?? tokenManagementOptions.Value.DefaultCacheMargin;
        TimeSpan cacheDuration = TimeSpan.FromSeconds(tokenResponse.ExpiresIn) - margin;

        if (cacheDuration > TimeSpan.Zero)
        {
            await tokenCache
                .SetTokenAsync(ClientName, tokenResponse.AccessToken, cacheDuration, cancellationToken)
                .ConfigureAwait(false);
        }

        LogTokenAcquired(ClientName, tokenResponse.ExpiresIn, cacheDuration);
        return tokenResponse.AccessToken;
    }

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

    private DPoPOptions? CreateDPoPOptions(ClientCredentialsOptions options)
    {
        if (!options.UseDPoP)
        {
            return null;
        }

        lock (_dpopLock)
        {
            _dpopPrivateKeyJwk ??= dpopProofService.GenerateKeyPair();
            return new DPoPOptions(_dpopPrivateKeyJwk, _dpopNonce);
        }
    }

    private IClientAuthenticationStrategy? CreateClientAuthStrategy(ClientCredentialsOptions options) =>
        options.ClientAuthenticationMethod switch
        {
            ClientAuthenticationMethod.ClientSecretPost when options.ClientSecret is not null
                => new ClientSecretPostStrategy(options.ClientSecret),
            ClientAuthenticationMethod.PrivateKeyJwt when options.ClientSigningKeyJwk is not null
                => new PrivateKeyJwtStrategy(options.ClientSigningKeyJwk, clock),
            _ => null,
        };

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage original,
        CancellationToken cancellationToken)
    {
        HttpRequestMessage clone = new(original.Method, original.RequestUri)
        {
            Version = original.Version,
        };

        foreach (KeyValuePair<string, IEnumerable<string>> header in original.Headers)
        {
            if (!string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(header.Key, "DPoP", StringComparison.OrdinalIgnoreCase))
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (original.Content is not null)
        {
            byte[] contentBytes = await original.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            clone.Content = new ByteArrayContent(contentBytes);

            if (original.Content.Headers.ContentType is not null)
            {
                clone.Content.Headers.ContentType = original.Content.Headers.ContentType;
            }
        }

        return clone;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Acquired client credentials token for {ClientName}: expires_in={ExpiresIn}s, cache_duration={CacheDuration}")]
    private partial void LogTokenAcquired(string clientName, int expiresIn, TimeSpan cacheDuration);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to acquire client credentials token for {ClientName}: {Error} — {ErrorDescription}")]
    private partial void LogTokenAcquisitionFailure(string clientName, string error, string? errorDescription);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Received 401 for {ClientName} at {RequestUri}, retrying with fresh token")]
    private partial void LogUnauthorizedRetry(string clientName, string requestUri);
}

#pragma warning restore GRSEC003
