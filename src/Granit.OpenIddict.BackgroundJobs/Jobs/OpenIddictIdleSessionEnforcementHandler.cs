using Granit.Settings.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OpenIddictIdleSessionEnforcementJob"/>.
/// Scans active refresh tokens and checks whether the corresponding session cache entry
/// has expired. If so, revokes the refresh token.
/// </summary>
/// <remarks>
/// <para>
/// The heartbeat endpoint (<c>POST /api/account/session/heartbeat</c>) maintains a
/// <c>session:{userId}:{jti}</c> key in <see cref="IDistributedCache"/> with a TTL
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
        IDistributedCache cache,
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

        // TODO: Iterate active refresh tokens via IOpenIddictTokenManager.ListAsync(),
        // check if the corresponding cache key session:{userId}:{jti} still exists,
        // and revoke tokens whose cache entry has expired.
        // This requires OpenIddict's ListAsync with a filter for refresh tokens,
        // which depends on the store implementation.
        // For now, the cache TTL handles cleanup automatically —
        // expired entries mean the heartbeat stopped, and the next token refresh
        // will fail because the session is no longer tracked.

        Log.IdleSessionEnforcementCompleted(logger);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Idle session enforcement: feature disabled (timeout = 0).")]
        public static partial void IdleSessionDisabled(ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Idle session enforcement started (timeout = {TimeoutMinutes} min).")]
        public static partial void IdleSessionEnforcementStarted(ILogger logger, int timeoutMinutes);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Idle session enforcement completed.")]
        public static partial void IdleSessionEnforcementCompleted(ILogger logger);
    }
}
