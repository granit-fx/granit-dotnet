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
            string? type = await tokenManager.GetTypeAsync(token, cancellationToken)
                .ConfigureAwait(false);
            string? status = await tokenManager.GetStatusAsync(token, cancellationToken)
                .ConfigureAwait(false);

            if (type != OpenIddictConstants.TokenTypeHints.RefreshToken
                || status != OpenIddictConstants.Statuses.Valid)
            {
                continue;
            }

            string? sessionId = await tokenManager.GetIdAsync(token, cancellationToken)
                .ConfigureAwait(false);
            if (sessionId is null)
            {
                continue;
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

            string? appId = await tokenManager.GetApplicationIdAsync(token, cancellationToken)
                .ConfigureAwait(false);
            if (appId is not null && !appNameCache.ContainsKey(appId))
            {
                object? app = await applicationManager.FindByIdAsync(appId, cancellationToken)
                    .ConfigureAwait(false);
                appNameCache[appId] = app is not null
                    ? await applicationManager.GetDisplayNameAsync(app, cancellationToken)
                        .ConfigureAwait(false)
                    : null;
            }

            IReadOnlyList<string> clients = appId is not null
                && appNameCache.TryGetValue(appId, out string? name) && name is not null
                ? [name] : [];

            sessions.Add(new IdentitySession(sessionId, ipAddress, startedAt, lastAccess,
                RememberMe: false, clients));
        }

        return sessions;
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
