using Granit.BackgroundJobs;

namespace Granit.Activities.BackgroundJobs.Jobs;

/// <summary>
/// Recurring scanner that emits an
/// <see cref="Events.ActivityReminderDueEvent"/> for every
/// <see cref="Domain.Activity"/> due tomorrow.
/// </summary>
/// <remarks>
/// Runs daily at 08:00 UTC. Single-fire per (activity, day) — there is no
/// stamp on the row; the cron + day-window query naturally dedupe within the
/// scan cadence. Hosts that need a per-tenant reminder time should override
/// the cron expression at registration.
/// </remarks>
[RecurringJob("0 8 * * *", "activities-send-reminders")]
public sealed record SendRemindersJob : IBackgroundJob;
