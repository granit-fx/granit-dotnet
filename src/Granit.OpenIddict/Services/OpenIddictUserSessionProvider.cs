using System.Collections.Immutable;
using System.Security.Claims;
using System.Text.Json;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Identity;
using Granit.OpenIddict.Extensions;
using Granit.Timing;
using OpenIddict.Abstractions;

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
    IUserSessionActivityStore activityStore,
    IClock clock,
    IDataFilter? dataFilter = null) : IUserSessionProvider, IUserDeviceProvider
{
    private static readonly TimeSpan HeartbeatDebounce = TimeSpan.FromMinutes(1);

    public async Task<IReadOnlyList<UserSessionDescriptor>> ListAsync(
        string userId, string? currentSessionId, CancellationToken cancellationToken = default)
    {
        var sessions = new List<UserSessionDescriptor>();

        // Kept deliberately: tokens themselves are global (TenantId stays null) and the
        // null-is-global filter would surface them anyway, but this listing also resolves each
        // token's application for its device kind, and applications may be tenant-owned. The
        // /sessions call can run outside the owning tenant's scope, so disable the IMultiTenant
        // filter for the whole listing to resolve those applications regardless of scope.
        using IDisposable? _ = dataFilter?.Disable<IMultiTenant>();

        // One batched read of last-activity per session (refresh token id), instead of one lookup per
        // token — mirrors the BFF reading BffTokenSet.LastAccessedAt.
        IReadOnlyDictionary<string, DateTimeOffset> activities =
            await activityStore.GetActivitiesAsync(userId, cancellationToken).ConfigureAwait(false);

        // One device-kind resolution per distinct client across the whole listing — many of a user's tokens
        // typically belong to the same application, so cache the resolved kind by application id.
        var kindByApplicationId = new Dictionary<string, DeviceKind>(StringComparer.Ordinal);

        await foreach (object token in tokenManager.FindBySubjectAsync(userId, cancellationToken))
        {
            UserSessionDescriptor? session = await MapTokenToSessionAsync(
                userId, token, currentSessionId, kindByApplicationId, activities, cancellationToken).ConfigureAwait(false);
            if (session is not null)
            {
                sessions.Add(session);
            }
        }

        return sessions;
    }

    public async Task<IReadOnlyList<UserDevice>> ListAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UserSessionDescriptor> sessions =
            await ListAsync(userId, currentSessionId: null, cancellationToken).ConfigureAwait(false);

        // The OpenIddict backend exposes no stable device id, OS or browser — only the raw IP. Synthesize one
        // device per IP from its own sessions; the kind carried on each session (declared on the OIDC
        // application, heuristic otherwise) drives the group's device kind.
        return UserDeviceGrouping.ByIpAddress(sessions);
    }

    /// <inheritdoc/>
    public async Task TouchAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // The access token's authorization is shared with the session's refresh token — record the
        // activity on the refresh token through it. No authorization (e.g. a client-credentials
        // token) means there is no interactive session to touch.
        if (!Guid.TryParse(principal.GetAuthorizationId(), out Guid authorizationId))
        {
            return;
        }

        await activityStore.TouchAsync(authorizationId, clock.Now, HeartbeatDebounce, cancellationToken)
            .ConfigureAwait(false);
    }

    // Maps a single OpenIddict token to a UserSessionDescriptor, or null when the token is not a
    // valid refresh token (the only kind that represents a live session) or carries no id.
    private async Task<UserSessionDescriptor?> MapTokenToSessionAsync(
        string userId, object token, string? currentSessionId,
        Dictionary<string, DeviceKind> kindByApplicationId,
        IReadOnlyDictionary<string, DateTimeOffset> activities,
        CancellationToken cancellationToken)
    {
        string? sessionId = await GetValidRefreshTokenIdAsync(token, cancellationToken)
            .ConfigureAwait(false);
        if (sessionId is null)
        {
            return null;
        }

        DateTimeOffset startedAt = (await tokenManager.GetCreationDateAsync(token, cancellationToken)
            .ConfigureAwait(false)) ?? clock.Now;

        DateTimeOffset lastAccess = activities.TryGetValue(sessionId, out DateTimeOffset la)
            ? la
            : startedAt;

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
        // No IMultiTenant bypass needed: this path only reads the token (tokens are always
        // global — TenantId stays null, never stamped), and the null-is-global query filter
        // makes a global row visible under every tenant scope. Ownership is enforced by the
        // subject check below.
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
        foreach (string sessionId in sessions.Select(s => s.SessionId))
        {
            if (sessionId == currentSessionId)
            {
                continue;
            }

            if (await RevokeAsync(userId, sessionId, cancellationToken).ConfigureAwait(false))
            {
                revoked++;
            }
        }

        return revoked;
    }
}
