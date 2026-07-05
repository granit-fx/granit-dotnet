using System.Diagnostics;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Options;
using Granit.Diagnostics;
using Granit.Timing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.ApiKeys.Internal;

/// <summary>
/// ASP.NET Core authentication handler that validates API keys from the
/// <c>Authorization: Bearer gk_*</c> header.
/// </summary>
/// <remarks>
/// <para>
/// Produces a <see cref="ClaimsPrincipal"/> with the same permission claim type
/// used by JWT Bearer, so existing <c>[Authorize]</c> policies work without modification.
/// </para>
/// <para>
/// Supports optional caching via <see cref="IApiKeyCacheService"/> (soft dependency
/// on <c>Granit.Caching</c>) and IP whitelisting via CIDR ranges.
/// </para>
/// </remarks>
internal sealed partial class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyStore store,
    IApiKeyHasher hasher,
    IClock clock,
    IApiKeyCacheService? cacheService = null)
    : AuthenticationHandler<ApiKeyOptions>(options, logger, encoder)
{
    private readonly IApiKeyHasher _hasher = hasher;

    /// <inheritdoc/>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? authorization = Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(authorization))
        {
            return AuthenticateResult.NoResult();
        }

        // Extract the token from "Bearer gk_..."
        string? token = ExtractToken(authorization);
        if (token is null)
        {
            return AuthenticateResult.NoResult();
        }

        // Try every accepted hash variant (v2-then-v1 when peppered, v1 only otherwise).
        // Returns on the first hit so the cache absorbs the common case to a single lookup.
        ApiKeyEntry? apiKey = null;
        foreach (string candidate in _hasher.ComputeCandidateHashes(token))
        {
            apiKey = await ResolveApiKeyAsync(candidate, Context.RequestAborted)
                .ConfigureAwait(false);
            if (apiKey is not null)
            {
                break;
            }
        }

        if (apiKey is null)
        {
            LogApiKeyNotFound(Logger);
            await TryWriteFailureAuditAsync("invalid_api_key", apiKeyId: null, apiKeyName: null, tenantId: null)
                .ConfigureAwait(false);
            return AuthenticateResult.Fail("Invalid API key.");
        }

        // Validate lifecycle
        DateTimeOffset now = clock.Now;

        if (apiKey.RevokedAt.HasValue)
        {
            LogApiKeyRevoked(Logger, apiKey.Id);
            await TryWriteFailureAuditAsync("api_key_revoked", apiKey.Id, apiKey.Name, apiKey.TenantId)
                .ConfigureAwait(false);
            return AuthenticateResult.Fail("API key has been revoked.");
        }

        if (apiKey.ExpiresAt <= now)
        {
            LogApiKeyExpired(Logger, apiKey.Id);
            await TryWriteFailureAuditAsync("api_key_expired", apiKey.Id, apiKey.Name, apiKey.TenantId)
                .ConfigureAwait(false);
            return AuthenticateResult.Fail("API key has expired.");
        }

        // Validate CIDR
        if (!CidrValidator.IsAllowed(Context.Connection.RemoteIpAddress, apiKey.AllowedCidrs))
        {
            LogIpNotAllowed(Logger, apiKey.Id, LogRedaction.IpAddress(Context.Connection.RemoteIpAddress?.ToString() ?? "unknown"));
            await TryWriteFailureAuditAsync("ip_not_allowed", apiKey.Id, apiKey.Name, apiKey.TenantId)
                .ConfigureAwait(false);
            return AuthenticateResult.Fail("IP address not in allowed CIDR ranges.");
        }

        // Build claims principal
        ClaimsPrincipal principal = BuildClaimsPrincipal(apiKey);

        // Track last used (fire-and-forget)
        if (Options.TrackLastUsed)
        {
            _ = store.UpdateLastUsedAsync(apiKey.Id, now, CancellationToken.None);
        }

        LogApiKeyAuthenticated(Logger, apiKey.Id, apiKey.Name);

        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationDefaults.AuthenticationScheme);
        return AuthenticateResult.Success(ticket);
    }

    private static string? ExtractToken(string authorization)
    {
        // Support "Bearer gk_..." and raw "gk_..." formats
        const string bearerPrefix = "Bearer ";

        if (authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string token = authorization[bearerPrefix.Length..].Trim();
            return token.StartsWith(ApiKeyAuthenticationDefaults.KeyPrefix, StringComparison.Ordinal)
                ? token
                : null;
        }

        return authorization.StartsWith(ApiKeyAuthenticationDefaults.KeyPrefix, StringComparison.Ordinal)
            ? authorization
            : null;
    }

    private async Task<ApiKeyEntry?> ResolveApiKeyAsync(string hashedKey, CancellationToken cancellationToken)
    {
        if (cacheService is not null)
        {
            return await cacheService.GetOrLoadAsync(
                    hashedKey,
                    ct => store.FindByHashAsync(hashedKey, ct),
                    Options.CacheDuration,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await store.FindByHashAsync(hashedKey, cancellationToken).ConfigureAwait(false);
    }

    private static ClaimsPrincipal BuildClaimsPrincipal(ApiKeyEntry apiKey)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, apiKey.Id.ToString()),
            new(ClaimTypes.Name, apiKey.Name),
            new(ApiKeyClaimTypes.ActorKind, nameof(Users.ActorKind.ExternalSystem)),
            new(ApiKeyClaimTypes.ApiKeyId, apiKey.Id.ToString()),
            new(ApiKeyClaimTypes.ApiKeyType, apiKey.Type.ToString()),
            new(ApiKeyClaimTypes.Environment, apiKey.Environment),
        ];

        foreach (string permission in apiKey.Permissions)
        {
            claims.Add(new Claim(ApiKeyClaimTypes.Permission, permission));
        }

        if (apiKey.TenantId.HasValue)
        {
            claims.Add(new Claim(ApiKeyClaimTypes.TenantId, apiKey.TenantId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Records an <see cref="AuditCategory.AccessDenied"/> audit row for a rejected API-key
    /// authentication. No-op when <see cref="IAuditingWriter"/> is not registered. Successful
    /// authentications are intentionally not audited per-request: a key authenticates on every
    /// call, so what it does is attributed via <c>CreatedBy</c> on the entities it touches.
    /// Audit failures are swallowed so a transient audit-store outage never blocks the request.
    /// </summary>
    private async Task TryWriteFailureAuditAsync(string reason, Guid? apiKeyId, string? apiKeyName, Guid? tenantId)
    {
        IAuditingWriter? auditingWriter = Context.RequestServices?.GetService<IAuditingWriter>();
        if (auditingWriter is null)
        {
            return;
        }

        string? ipAddress = Context.Connection.RemoteIpAddress?.ToString();
        string? userAgent = Request.Headers.UserAgent.ToString();
        if (string.IsNullOrEmpty(userAgent))
        {
            userAgent = null;
        }

        AuditEntry entry = AuthenticationAuditEntry.CreateFailure(
            clock.Now,
            userId: apiKeyId?.ToString(),
            userName: apiKeyName,
            method: "api_key",
            reason: reason,
            tenantId: tenantId,
            ipAddress: ipAddress,
            userAgent: userAgent,
            correlationId: Activity.Current?.Id);

        try
        {
            await auditingWriter.WriteAsync(entry, Context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAuditWriteFailed(Logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "API key not found for provided token hash.")]
    private static partial void LogApiKeyNotFound(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "API key authentication: failed to write AccessDenied audit entry — request continues.")]
    private static partial void LogAuditWriteFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "API key {ApiKeyId} has been revoked.")]
    private static partial void LogApiKeyRevoked(ILogger logger, Guid apiKeyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "API key {ApiKeyId} has expired.")]
    private static partial void LogApiKeyExpired(ILogger logger, Guid apiKeyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "API key {ApiKeyId}: IP {MaskedIp} not in allowed CIDR ranges.")]
    private static partial void LogIpNotAllowed(ILogger logger, Guid apiKeyId, string? maskedIp);

    [LoggerMessage(Level = LogLevel.Debug, Message = "API key {ApiKeyId} ({ApiKeyName}) authenticated successfully.")]
    private static partial void LogApiKeyAuthenticated(ILogger logger, Guid apiKeyId, string apiKeyName);
}
