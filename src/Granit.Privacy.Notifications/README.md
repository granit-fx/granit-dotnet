# Granit.Privacy.Notifications

Notification bridge for `Granit.Privacy`. Sends deletion reminder and confirmation
emails via `Granit.Notifications` when using the GDPR Art. 17 cooling-off period,
and exposes the data controller and DPO contact (GDPR Art. 13) as the
`{{ privacy }}` template global context for any rendered template.

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
