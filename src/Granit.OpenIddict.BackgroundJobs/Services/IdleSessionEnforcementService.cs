using Granit.Identity.Local.Services;
using Granit.Settings.Services;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.OpenIddict.BackgroundJobs.Services;

/// <summary>
/// Scans active refresh tokens and checks whether the corresponding session cache entry
/// has expired. If so, revokes the refresh token.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Temporary safe-guard:</strong> revocation is intentionally disabled. The previous
/// implementation probed a <see cref="IFusionCache"/> key <c>session:{subject}:{refreshTokenId}</c>
/// that the heartbeat endpoint never wrote under the same identifier (it writes
/// <c>session:{subject}:{accessTokenJti}</c>), so every refresh token looked idle and was
/// revoked on the first run — a mass logout — and <c>remember_me</c> sessions (which the
/// heartbeat deliberately never tracks) were revoked too. Until the correct mechanism ships
/// (a persistent last-activity column on the token entry, replacing this three-party cache
/// contract), the job scans and reports what it <em>would</em> revoke but does not revoke,
/// favouring "no enforcement" over "log everyone out".
/// </para>
/// </remarks>
public sealed partial class IdleSessionEnforcementService(
    IOpenIddictTokenManager tokenManager,
    IFusionCache cache,
    ISettingProvider settingProvider,
    ILogger<IdleSessionEnforcementService> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
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

        // SAFE-GUARD: the cache-key contract below is broken (the heartbeat writes
        // session:{subject}:{accessTokenJti} while this scan probes
        // session:{subject}:{refreshTokenId}), so every entry looks idle. We scan and count
        // what the old logic WOULD have revoked, but do NOT revoke — a cache miss here is not
        // trustworthy evidence of idleness. The real mechanism (persistent last-activity on the
        // token entry) replaces this loop wholesale; see the class remarks.
        const int pageSize = 1_000;
        int wouldRevokeCount = 0;
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

                string cacheKey = $"session:{subject}:{tokenId}";
                MaybeValue<UserSessionActivity> cachedEntry = await cache
                    .TryGetAsync<UserSessionActivity>(cacheKey, token: cancellationToken).ConfigureAwait(false);

                if (!cachedEntry.HasValue)
                {
                    // Would revoke under the old (broken) contract — counted only, never revoked.
                    wouldRevokeCount++;
                }
            }

            offset += pageCount;
            hasMore = pageCount == pageSize;
        }
        while (hasMore);

        Log.IdleSessionEnforcementDeferred(logger, wouldRevokeCount);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Idle session enforcement: feature disabled (timeout = 0).")]
        public static partial void IdleSessionDisabled(ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Idle session enforcement started (timeout = {TimeoutMinutes} min).")]
        public static partial void IdleSessionEnforcementStarted(ILogger logger, int timeoutMinutes);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Idle session enforcement is temporarily disabled (broken cache-key contract). Would have revoked {WouldRevokeCount} refresh token(s); none were revoked. Pending the persistent last-activity mechanism.")]
        public static partial void IdleSessionEnforcementDeferred(ILogger logger, int wouldRevokeCount);
    }
}
