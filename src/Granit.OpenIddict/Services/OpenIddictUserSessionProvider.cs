using System.Collections.Immutable;
using System.Text.Json;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Identity;
using Granit.Identity.Local.Services;
using Granit.OpenIddict.Extensions;
using Granit.Timing;
using OpenIddict.Abstractions;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.OpenIddict.Services;

/// <summary>
/// OpenIddict-backed <see cref="IUserSessionProvider"/> / <see cref="IUserDeviceProvider"/>: each
/// valid refresh token in the OpenIddict token store represents one live session, identified by the
/// token id. Revocation calls into <see cref="IOpenIddictTokenManager"/> to actually revoke the
/// underlying refresh token, so the session disappears on the next token-refresh attempt.
/// </summary>
internal sealed class OpenIddictUserSessionProvider(
    IOpenIddictTokenManager tokenManager,
    IOpenIddictApplicationManager applicationManager,
    IFusionCache cache,
    IClock clock,
    IDataFilter? dataFilter = null) : IUserSessionProvider, IUserDeviceProvider
{
    public async Task<IReadOnlyList<UserSessionDescriptor>> ListAsync(
        string userId, string? currentSessionId, CancellationToken cancellationToken = default)
    {
        var sessions = new List<UserSessionDescriptor>();

        // GranitOpenIddictToken.TenantId is always null — OpenIddict never sets it.
        // Disable the IMultiTenant filter so tenant users can see their own tokens.
        using IDisposable? _ = dataFilter?.Disable<IMultiTenant>();

        // One device-kind resolution per distinct client across the whole listing — many of a user's tokens
        // typically belong to the same application, so cache the resolved kind by application id.
        var kindByApplicationId = new Dictionary<string, DeviceKind>(StringComparer.Ordinal);

        await foreach (object token in tokenManager.FindBySubjectAsync(userId, cancellationToken))
        {
            UserSessionDescriptor? session = await MapTokenToSessionAsync(
                userId, token, currentSessionId, kindByApplicationId, cancellationToken).ConfigureAwait(false);
            if (session is not null)
            {
                sessions.Add(session);
            }
        }

        return sessions;
    }

    // Maps a single OpenIddict token to a UserSessionDescriptor, or null when the token is not a
    // valid refresh token (the only kind that represents a live session) or carries no id.
    private async Task<UserSessionDescriptor?> MapTokenToSessionAsync(
        string userId, object token, string? currentSessionId,
        Dictionary<string, DeviceKind> kindByApplicationId, CancellationToken cancellationToken)
    {
        string? sessionId = await GetValidRefreshTokenIdAsync(token, cancellationToken)
            .ConfigureAwait(false);
        if (sessionId is null)
        {
            return null;
        }

        DateTimeOffset startedAt = (await tokenManager.GetCreationDateAsync(token, cancellationToken)
            .ConfigureAwait(false)) ?? clock.Now;

        MaybeValue<UserSessionActivity> activity =
            await cache.TryGetAsync<UserSessionActivity>(
                $"session:{userId}:{sessionId}", token: cancellationToken)
                .ConfigureAwait(false);
        DateTimeOffset lastAccess = activity.HasValue ? activity.Value.LastActivityAt : startedAt;

        ImmutableDictionary<string, JsonElement> properties =
            await tokenManager.GetPropertiesAsync(token, cancellationToken).ConfigureAwait(false);
        string? ipAddress = GetStringProperty(properties, "ip_address");
        string? userAgent = GetStringProperty(properties, "user_agent");

        DeviceKind kind = await ResolveDeviceKindAsync(token, kindByApplicationId, cancellationToken)
            .ConfigureAwait(false);

        return new UserSessionDescriptor(
            SessionId: sessionId,
            UserId: userId,
            IsCurrent: sessionId == currentSessionId,
            CreatedAt: startedAt,
            LastAccessedAt: lastAccess,
            UserAgent: userAgent,
            IpAddress: ipAddress,
            Location: null,
            Kind: kind);
    }

    // Resolves the device kind for the token's client: the application's declared DeviceKind, else a
    // redirect-URI/grant-type heuristic. Returns Unknown only when the token carries no resolvable application.
    private async ValueTask<DeviceKind> ResolveDeviceKindAsync(
        object token, Dictionary<string, DeviceKind> kindByApplicationId, CancellationToken cancellationToken)
    {
        string? applicationId = await tokenManager.GetApplicationIdAsync(token, cancellationToken)
            .ConfigureAwait(false);
        if (applicationId is null)
        {
            return DeviceKind.Unknown;
        }

        if (kindByApplicationId.TryGetValue(applicationId, out DeviceKind cached))
        {
            return cached;
        }

        DeviceKind resolved = DeviceKind.Unknown;
        object? application = await applicationManager.FindByIdAsync(applicationId, cancellationToken)
            .ConfigureAwait(false);
        if (application is not null)
        {
            resolved = await applicationManager.GetDeviceKindAsync(application, cancellationToken).ConfigureAwait(false)
                ?? await InferDeviceKindAsync(application, cancellationToken).ConfigureAwait(false);
        }

        kindByApplicationId[applicationId] = resolved;
        return resolved;
    }

    // Fallback when the client declares no DeviceKind: classify from the authentication context. An
    // extension redirect URI marks a BrowserExtension; the device-authorization grant marks a Tv-class
    // input-constrained device; a client-credentials-only grant marks an ApiClient; otherwise Browser.
    private async ValueTask<DeviceKind> InferDeviceKindAsync(
        object application, CancellationToken cancellationToken)
    {
        ImmutableArray<string> redirectUris = await applicationManager
            .GetRedirectUrisAsync(application, cancellationToken).ConfigureAwait(false);
        if (redirectUris.Any(static uri =>
                uri.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase)
                || uri.StartsWith("moz-extension://", StringComparison.OrdinalIgnoreCase)))
        {
            return DeviceKind.BrowserExtension;
        }

        ImmutableArray<string> permissions = await applicationManager
            .GetPermissionsAsync(application, cancellationToken).ConfigureAwait(false);
        if (permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.DeviceCode))
        {
            return DeviceKind.Tv;
        }

        if (permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials))
        {
            return DeviceKind.ApiClient;
        }

        return DeviceKind.Browser;
    }

    // Returns the token id when the token is a valid refresh token (the only kind representing a
    // live session) carrying an id; otherwise null.
    private async Task<string?> GetValidRefreshTokenIdAsync(
        object token, CancellationToken cancellationToken)
    {
        string? type = await tokenManager.GetTypeAsync(token, cancellationToken)
            .ConfigureAwait(false);
        string? status = await tokenManager.GetStatusAsync(token, cancellationToken)
            .ConfigureAwait(false);

        if (type != OpenIddictConstants.TokenTypeHints.RefreshToken
            || status != OpenIddictConstants.Statuses.Valid)
        {
            return null;
        }

        return await tokenManager.GetIdAsync(token, cancellationToken).ConfigureAwait(false);
    }

    private static string? GetStringProperty(
        ImmutableDictionary<string, JsonElement> properties, string key) =>
        properties.TryGetValue(key, out JsonElement el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    public async Task<bool> RevokeAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        using IDisposable? _ = dataFilter?.Disable<IMultiTenant>();

        object? token = await tokenManager.FindByIdAsync(sessionId, cancellationToken)
            .ConfigureAwait(false);
        if (token is null)
        {
            return false;
        }

        // Only revoke a live refresh token belonging to this user — never another subject's token,
        // and never an already-revoked/expired one (a no-op revoke must report false).
        if (await GetValidRefreshTokenIdAsync(token, cancellationToken).ConfigureAwait(false) is null)
        {
            return false;
        }

        string? subject = await tokenManager.GetSubjectAsync(token, cancellationToken)
            .ConfigureAwait(false);
        if (subject != userId)
        {
            return false;
        }

        return await tokenManager.TryRevokeAsync(token, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> RevokeOthersAsync(
        string userId, string currentSessionId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UserSessionDescriptor> sessions =
            await ListAsync(userId, currentSessionId, cancellationToken).ConfigureAwait(false);

        int revoked = 0;
        foreach (UserSessionDescriptor session in sessions)
        {
            if (session.SessionId == currentSessionId)
            {
                continue;
            }

            if (await RevokeAsync(userId, session.SessionId, cancellationToken).ConfigureAwait(false))
            {
                revoked++;
            }
        }

        return revoked;
    }

    public async Task<IReadOnlyList<UserDevice>> ListAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UserSessionDescriptor> sessions =
            await ListAsync(userId, currentSessionId: null, cancellationToken).ConfigureAwait(false);

        // The OpenIddict backend exposes no stable device id, OS or browser — only the raw IP. Group
        // sessions by IP and synthesize a stable device signature from it; the kind comes from the client
        // the session authenticated through (declared on the OIDC application, heuristic otherwise).
        return [.. sessions
            .GroupBy(s => s.IpAddress ?? "unknown")
            .Select(g => new UserDevice(
                DeviceId: g.Key,
                Kind: ResolveGroupKind(g),
                OperatingSystem: null,
                Browser: null,
                LastSeen: g.Max(s => s.LastAccessedAt),
                SessionCount: g.Count(),
                LastLocation: null))];
    }

    // A "device" here is an IP that may have sessions from several clients. Surface the most specific kind
    // seen (a BrowserExtension/Tv/ApiClient is more informative than a plain browser), defaulting to Browser
    // rather than Unknown — an IdP-SSO device is browser-based unless something more specific is known.
    private static DeviceKind ResolveGroupKind(IEnumerable<UserSessionDescriptor> group)
    {
        foreach (DeviceKind kind in group.Select(s => s.Kind))
        {
            if (kind is not DeviceKind.Unknown and not DeviceKind.Browser)
            {
                return kind;
            }
        }

        return DeviceKind.Browser;
    }
}
