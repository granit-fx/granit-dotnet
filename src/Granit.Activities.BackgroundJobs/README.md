# Granit.Activities.BackgroundJobs

Background jobs for [Granit.Activities](../Granit.Activities/README.md) —
two recurring scanners that drive the assignee-facing notifications shipped
in [Granit.Activities.Notifications](../Granit.Activities.Notifications/README.md).

| Job | Cron | What it does |
| --- | ---- | ------------ |
| `MarkOverdueJob` | `*/5 * * * *` (every 5 min) | Scans `Status == Open && DueAt < clock.Now - 1h && OverdueNotifiedAt is null`, emits one `ActivityOverdueEvent` per row, stamps `OverdueNotifiedAt` for idempotency |
| `SendRemindersJob` | `0 8 * * *` (daily 08:00 UTC) | Scans `Status == Open && DueAt within tomorrow's day-window`, emits one `ActivityReminderDueEvent` per row |

## Wire-up

```csharp
// In the host's Program.cs — add the BG jobs module after the runtime + EF.
builder.Services
    .AddGranitActivities()
    .AddGranitActivitiesEntityFrameworkCore(opts => opts.UseNpgsql(connStr))
    .AddGranitActivitiesNotifications();   // story A7 — handlers for the events these jobs emit
// The recurring jobs are auto-discovered by Granit.BackgroundJobs at boot.
```

## Idempotency

`MarkOverdueJob` writes `OverdueNotifiedAt` after publishing the event. The
next scan excludes rows where the stamp is non-null, so the same activity is
notified at most once. If publication fails (transient bus error), the stamp
is not written and the next scan retries — at-least-once delivery for the
assignee notification.

`SendRemindersJob` does not stamp anything — the daily cadence + the
tomorrow-only day-window query naturally dedupe within the scan period.
Re-emission after a reschedule into the same window is intentional (the
user gets a fresh reminder for the new due date).

## Grace period

`MarkOverdueJob` applies a 1-hour grace before treating an activity as
overdue. This prevents racing the assignee who completes the activity within
the same poll cycle as their due date — without it, an activity due at
14:00 completed at 14:02 would race the 14:05 scan.
