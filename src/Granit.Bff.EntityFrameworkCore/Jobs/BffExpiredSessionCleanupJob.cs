using Granit.BackgroundJobs;

namespace Granit.Bff.EntityFrameworkCore.Jobs;

/// <summary>
/// Recurring job that purges expired BFF sessions from the database.
/// SQL databases have no native TTL — this job runs every 15 minutes to reclaim storage.
/// </summary>
/// <remarks>
/// Cron overridable via <c>BackgroundJobs:Jobs:bff-expired-session-cleanup</c>.
/// </remarks>
[RecurringJob("*/15 * * * *", "bff-expired-session-cleanup")]
public sealed record BffExpiredSessionCleanupJob : IBackgroundJob;
