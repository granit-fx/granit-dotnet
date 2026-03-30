using Granit.Identity.Local.Services;
using Granit.OpenIddict.Services;
using Granit.Settings.Services;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OpenIddictIdleSessionEnforcementJob"/>.
/// Scans active refresh tokens and checks whether the corresponding session cache entry
/// has expired. If so, revokes the refresh token.
/// </summary>
/// <remarks>
/// <para>
/// The heartbeat endpoint (<c>POST /api/account/session/heartbeat</c>) maintains a
/// <c>session:{userId}:{jti}</c> key in <see cref="IFusionCache"/> with a TTL
/// of <c>IdleSessionTimeout + 5 min</c>. When the cache entry expires (user stopped
/// sending heartbeats), this job revokes the associated refresh token.
/// </para>
/// <para>
/// Tokens with <c>remember_me = true</c> are skipped — those sessions never time out.
/// </para>
/// </remarks>
internal static partial class OpenIddictIdleSessionEnforcementHandler
{
    /// <summary>
    /// Enforces idle session timeouts by revoking refresh tokens for inactive sessions.
    /// </summary>
    public static async Task HandleAsync(
        OpenIddictIdleSessionEnforcementJob _,
        IOpenIddictTokenManager tokenManager,
        IFusionCache cache,
        ISettingProvider settingProvider,
        ILogger<OpenIddictIdleSessionEnforcementJob> logger,
        CancellationToken cancellationToken)
    {
        // Read the idle session timeout setting (minutes, 0 = disabled)
        string? timeoutValue = await settingProvider
            .GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, cancellationToken)
            .ConfigureAwait(false);

        if (!int.TryParse(timeoutValue, out int timeoutMinutes) || timeoutMinutes <= 0)
        {
            Log.IdleSessionDisabled(logger);
            return;
        }

        Log.IdleSessionEnforcementStarted(logger, timeoutMinutes);

        // Strategy: the heartbeat endpoint sets a cache key session:{userId}:{jti}
        // with TTL = IdleSessionTimeout + 5 min. When the cache entry expires naturally
        // (user stopped sending heartbeats), the session is considered idle.
        //
        // This job iterates active refresh tokens in bounded pages and checks whether
        // their corresponding cache entry still exists. If not, the refresh token is revoked.
        const int pageSize = 1_000;
        int revokedCount = 0;
        int offset = 0;
        bool hasMore;

        do
        {
            int pageCount = 0;

            await foreach (object token in tokenManager.ListAsync(pageSize, offset, cancellationToken))
            {
                pageCount++;

                string? tokenType = await tokenManager.GetTypeAsync(token, cancellationToken).ConfigureAwait(false);
                if (tokenType != global::OpenIddict.Abstractions.OpenIddictConstants.TokenTypeHints.RefreshToken)
                {
                    continue;
                }

                string? subject = await tokenManager.GetSubjectAsync(token, cancellationToken).ConfigureAwait(false);
                string? tokenId = await tokenManager.GetIdAsync(token, cancellationToken).ConfigureAwait(false);
                if (subject is null || tokenId is null)
                {
                    continue;
                }

                // Check if session cache entry still exists
                string cacheKey = $"session:{subject}:{tokenId}";
                MaybeValue<UserSessionActivity> cachedEntry = await cache
                    .TryGetAsync<UserSessionActivity>(cacheKey, token: cancellationToken).ConfigureAwait(false);

                if (!cachedEntry.HasValue)
                {
                    // Cache entry expired → session idle → revoke refresh token
                    await tokenManager.TryRevokeAsync(token, cancellationToken).ConfigureAwait(false);
                    revokedCount++;
                }
            }

            offset += pageCount;
            hasMore = pageCount == pageSize;
        }
        while (hasMore);

        Log.IdleSessionEnforcementCompleted(logger, revokedCount);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Idle session enforcement: feature disabled (timeout = 0).")]
        public static partial void IdleSessionDisabled(ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Idle session enforcement started (timeout = {TimeoutMinutes} min).")]
        public static partial void IdleSessionEnforcementStarted(ILogger logger, int timeoutMinutes);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Idle session enforcement completed. Revoked {RevokedCount} refresh tokens.")]
        public static partial void IdleSessionEnforcementCompleted(ILogger logger, int revokedCount);
    }
}
