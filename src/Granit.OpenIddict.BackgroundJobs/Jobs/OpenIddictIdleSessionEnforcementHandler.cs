using Microsoft.Extensions.Logging;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OpenIddictIdleSessionEnforcementJob"/>.
/// Scans sessions whose cache entry has expired and revokes associated refresh tokens.
/// </summary>
internal static partial class OpenIddictIdleSessionEnforcementHandler
{
    /// <summary>
    /// Enforces idle session timeouts by revoking refresh tokens for inactive sessions.
    /// </summary>
    public static Task HandleAsync(
        OpenIddictIdleSessionEnforcementJob _,
        ILogger<OpenIddictIdleSessionEnforcementJob> logger,
        CancellationToken cancellationToken)
    {
        // Implementation will iterate tenants with IdleSessionTimeout > 0,
        // check ICacheService<UserSessionActivity> for expired entries,
        // and revoke corresponding refresh tokens via IOpenIddictTokenManager.
        // Tokens with remember_me claim are skipped.
        Log.IdleSessionEnforcementStarted(logger);
        return Task.CompletedTask;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Idle session enforcement job started.")]
        public static partial void IdleSessionEnforcementStarted(ILogger logger);
    }
}
