using System.Collections.Immutable;
using System.Text.Json;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Identity;
using Granit.Identity.Local.Services;
using Granit.Identity.Models;
using Granit.Timing;
using OpenIddict.Abstractions;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.OpenIddict.Services;

internal sealed class OpenIddictSessionManager(
    IOpenIddictTokenManager tokenManager,
    IOpenIddictApplicationManager applicationManager,
    IFusionCache cache,
    IClock clock,
    IDataFilter? dataFilter = null) : IIdentitySessionManager
{
    public async Task<IReadOnlyList<IdentitySession>> GetUserSessionsAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var sessions = new List<IdentitySession>();
        var appNameCache = new Dictionary<string, string?>();

        // GranitOpenIddictToken.TenantId is always null — OpenIddict never sets it.
        // Disable the IMultiTenant filter so tenant users can see their own tokens.
        using IDisposable? _ = dataFilter?.Disable<IMultiTenant>();

        await foreach (object token in tokenManager.FindBySubjectAsync(userId, cancellationToken))
        {
            IdentitySession? session = await MapTokenToSessionAsync(
                userId, token, appNameCache, cancellationToken).ConfigureAwait(false);
            if (session is not null)
            {
                sessions.Add(session);
            }
        }

        return sessions;
    }

    // Maps a single OpenIddict token to an IdentitySession, or null when the token is not a
    // valid refresh token (the only kind that represents a live session) or carries no id.
    private async Task<IdentitySession?> MapTokenToSessionAsync(
        string userId, object token, Dictionary<string, string?> appNameCache,
        CancellationToken cancellationToken)
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

        string? sessionId = await tokenManager.GetIdAsync(token, cancellationToken)
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
        string? ipAddress = properties.TryGetValue("ip_address", out JsonElement ipEl)
            && ipEl.ValueKind == JsonValueKind.String ? ipEl.GetString() : null;

        IReadOnlyList<string> clients = await ResolveClientNamesAsync(
            token, appNameCache, cancellationToken).ConfigureAwait(false);

        return new IdentitySession(sessionId, ipAddress, startedAt, lastAccess,
            RememberMe: false, clients);
    }

    // Resolves the display name of the token's client application, memoising lookups in
    // appNameCache so each application is fetched at most once per GetUserSessionsAsync call.
    private async Task<IReadOnlyList<string>> ResolveClientNamesAsync(
        object token, Dictionary<string, string?> appNameCache, CancellationToken cancellationToken)
    {
        string? appId = await tokenManager.GetApplicationIdAsync(token, cancellationToken)
            .ConfigureAwait(false);
        if (appId is null)
        {
            return [];
        }

        if (!appNameCache.ContainsKey(appId))
        {
            object? app = await applicationManager.FindByIdAsync(appId, cancellationToken)
                .ConfigureAwait(false);
            appNameCache[appId] = app is not null
                ? await applicationManager.GetDisplayNameAsync(app, cancellationToken)
                    .ConfigureAwait(false)
                : null;
        }

        return appNameCache.TryGetValue(appId, out string? name) && name is not null ? [name] : [];
    }

    public async Task<IReadOnlyList<IdentityDeviceActivity>> GetUserDeviceActivityAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IdentitySession> sessions =
            await GetUserSessionsAsync(userId, cancellationToken).ConfigureAwait(false);

        return [..sessions
            .GroupBy(s => s.IpAddress ?? "unknown")
            .Select(g => new IdentityDeviceActivity(
                IpAddress: g.Key == "unknown" ? null : g.Key,
                LastAccess: g.Max(s => s.LastAccess),
                Device: null, Os: null, OsVersion: null, Browser: null,
                Mobile: false, Current: false,
                Sessions: [..g]))];
    }

    public Task TerminateSessionAsync(string userId, string sessionId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task TerminateAllSessionsAsync(string userId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
