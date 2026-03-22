using Granit.BackgroundJobs;

namespace Granit.Privacy.Jobs;

/// <summary>
/// Recurring safety-net job that enforces deletion deadlines for deferred requests.
/// Runs daily at 2:00 AM and catches any requests that the <see cref="DataDeletion.GdprDeletionSaga"/>
/// might have missed (e.g., due to outbox issues).
/// </summary>
[RecurringJob("0 2 * * *", "privacy-deletion-deadline-enforcer")]
public sealed record DeletionDeadlineEnforcerJob : IBackgroundJob;
