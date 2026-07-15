using System.Collections.Immutable;
using System.Text.Json;
using Granit.OpenIddict.Services;
using Granit.Settings.Services;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.BackgroundJobs.Services;

/// <summary>
/// Revokes refresh tokens whose session has been idle beyond the configured timeout, so an
/// abandoned session cannot be resumed indefinitely. Remember-me sessions are exempt.
/// </summary>
/// <remarks>
/// Idleness is measured from the durable last-activity the heartbeat stamps on the refresh token
/// (<see cref="IUserSessionActivityStore"/>), falling back to the token's creation date for a session
/// that never sent a heartbeat. This replaces the earlier three-party FusionCache contract whose keys
/// never matched — which made every session look idle, so the job was disabled as a safe-guard against
/// a mass logout.
/// </remarks>
public sealed partial class IdleSessionEnforcementService(
    IOpenIddictTokenManager tokenManager,
    IUserSessionActivityStore activityStore,
    ISettingProvider settingProvider,
    IClock clock,
    ILogger<IdleSessionEnforcementService> logger)
{
    private const int BatchSize = 1_000;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Idle session timeout in minutes (0 or unset = disabled).
        string? timeoutValue = await settingProvider
            .GetOrNullAsync(OpenIddictSettingNames.IdleSessionTimeout, cancellationToken)
            .ConfigureAwait(false);

        if (!int.TryParse(timeoutValue, out int timeoutMinutes) || timeoutMinutes <= 0)
        {
            Log.IdleSessionDisabled(logger);
            return;
        }

        Log.IdleSessionEnforcementStarted(logger, timeoutMinutes);

        DateTimeOffset idleSince = clock.Now - TimeSpan.FromMinutes(timeoutMinutes);
        IReadOnlyList<string> idleIds = await activityStore
            .GetIdleRefreshTokenIdsAsync(idleSince, BatchSize, cancellationToken).ConfigureAwait(false);

        int revoked = 0;
        int exempt = 0;
        foreach (string tokenId in idleIds)
        {
            object? token = await tokenManager.FindByIdAsync(tokenId, cancellationToken).ConfigureAwait(false);
            if (token is null)
            {
                continue;
            }

            if (await IsRememberMeAsync(token, cancellationToken).ConfigureAwait(false))
            {
                // Persistent session — the user opted out of inactivity revocation.
                exempt++;
                continue;
            }

            if (await tokenManager.TryRevokeAsync(token, cancellationToken).ConfigureAwait(false))
            {
                revoked++;
            }
        }

        Log.IdleSessionEnforcementCompleted(logger, revoked, exempt);
    }

    private async Task<bool> IsRememberMeAsync(object token, CancellationToken cancellationToken)
    {
        ImmutableDictionary<string, JsonElement> properties = await tokenManager
            .GetPropertiesAsync(token, cancellationToken).ConfigureAwait(false);

        return properties.TryGetValue("remember_me", out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is "true";
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Idle session enforcement: feature disabled (timeout = 0).")]
        public static partial void IdleSessionDisabled(ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Idle session enforcement started (timeout = {TimeoutMinutes} min).")]
        public static partial void IdleSessionEnforcementStarted(ILogger logger, int timeoutMinutes);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Idle session enforcement completed: revoked {Revoked} idle refresh token(s), exempted {Exempt} remember-me session(s).")]
        public static partial void IdleSessionEnforcementCompleted(ILogger logger, int revoked, int exempt);
    }
}
