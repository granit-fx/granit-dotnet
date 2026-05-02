# Granit.Activities.Notifications

Notification bridge for [Granit.Activities](../Granit.Activities/README.md) —
sends three lifecycle notifications to the assignee via the framework's
notification publisher:

| Type | Trigger | Default channels |
| ---- | ------- | ---------------- |
| `activity.assigned` | `ActivityAssignedEvent` (raised by `Activity.Create` / `Reassign`) | Email + InApp |
| `activity.reminder` | `ActivityReminderDueEvent` (raised by the reminder background job — story A8) | Email + InApp |
| `activity.overdue` | `ActivityOverdueEvent` (raised by the overdue scan background job — story A8) | Email + InApp (severity = Warning) |

Pull `Granit.Activities.Notifications` from any host that wants the standard
behaviour. The handlers are registered automatically by the module class —
no extra DI wiring needed at the host.

## Templates

The package ships **EN + FR** as the framework baseline (per CLAUDE.md
§Translation strategy). Generated additional cultures via
`scripts/translate-templates.py` (story #1311) carry an
`<!-- AUTO-TRANSLATED -->` first-line marker. Apps can override any template
at runtime through the `Granit.Templating` admin API — the DB-backed resolver
runs at higher priority than the embedded one.

Each template's first line is `<title>...</title>` — the email channel
extracts the subject from this tag.

## Custom recipients / disabling a notification

Hosts that want a different recipient policy (for example, copying the
manager on overdue activities) can replace the handler at the DI level by
registering their own `ILocalEventHandler<ActivityOverdueEvent>` after
`AddGranit*Notifications()`.

To disable a notification entirely, replace the handler with a no-op or
remove the package — the events from `Granit.Activities` are emitted
unconditionally and harmlessly, regardless of whether anyone consumes them.
