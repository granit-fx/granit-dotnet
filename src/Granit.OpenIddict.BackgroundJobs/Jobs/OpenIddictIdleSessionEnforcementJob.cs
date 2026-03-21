using Granit.BackgroundJobs;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that revokes refresh tokens for idle sessions.
/// </summary>
/// <remarks>
/// Runs every 5 minutes. Checks <c>ICacheService&lt;UserSessionActivity&gt;</c> for expired
/// entries and revokes associated refresh tokens via <c>IOpenIddictTokenManager</c>.
/// Only active when <c>OpenIddict.IdleSessionTimeout &gt; 0</c> for the tenant.
/// </remarks>
[RecurringJob("*/5 * * * *", "openiddict-idle-session-enforcement")]
public sealed record OpenIddictIdleSessionEnforcementJob : IBackgroundJob;
