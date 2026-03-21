using Granit.BackgroundJobs;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that prunes expired OpenIddict tokens and authorizations.
/// </summary>
/// <remarks>
/// Scheduled every hour. Concurrency-safe via Wolverine Outbox.
/// Cron overridable via <c>BackgroundJobs:Jobs:openiddict-token-cleanup</c>.
/// </remarks>
[RecurringJob("0 * * * *", "openiddict-token-cleanup")]
public sealed record OpenIddictTokenCleanupJob : IBackgroundJob;
