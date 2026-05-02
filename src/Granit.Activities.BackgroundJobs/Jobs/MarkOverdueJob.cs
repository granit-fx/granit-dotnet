using Granit.BackgroundJobs;

namespace Granit.Activities.BackgroundJobs.Jobs;

/// <summary>
/// Recurring scanner that emits an
/// <see cref="Events.ActivityOverdueEvent"/> for every
/// <see cref="Domain.Activity"/> still <see cref="Domain.ActivityStatus.Open"/>
/// past <see cref="Domain.Activity.DueAt"/> that has not yet been notified.
/// </summary>
/// <remarks>
/// Runs every 5 minutes. Idempotent — per-row dedupe via
/// <see cref="Domain.Activity.OverdueNotifiedAt"/> stamped after the event is
/// raised, so the same activity is notified at most once per overdue cycle.
/// </remarks>
[RecurringJob("*/5 * * * *", "activities-mark-overdue")]
public sealed record MarkOverdueJob : IBackgroundJob;
