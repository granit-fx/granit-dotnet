# Granit.Privacy.Notifications

Notification bridge for `Granit.Privacy`. Covers the full lifecycle of data-subject
rights — request acknowledgement, deletion deferred / cancelled / executed reminders
and confirmations, export ready / partial — via `Granit.Notifications`. Ships
embedded HTML templates in **English and French** for all 8 notification types
(overridable at runtime through the `Granit.Templating` admin API). Exposes the
data controller and DPO contact (GDPR Art. 13) as the `{{ privacy }}` template
global context.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Privacy.Notifications
```

## Dependencies

- `Granit.Privacy`
- `Granit.Notifications`
- `Granit.Templating`

## Documentation

See the [full documentation](https://granit-fx.dev).
