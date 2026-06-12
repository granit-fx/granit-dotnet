# Granit.UserSessions.Notifications

Notification bridge for `Granit.UserSessions`. Consumes
`SuspiciousUserSessionDetectedEto` and sends the account owner a localized,
brand-neutral "suspicious sign-in" security alert email via `Granit.Notifications`
— telling the user **where** the sign-in came from (city and country, never the
raw IP) and **why** it was flagged, in plain language.

Two tiers, routed by the event's risk level:

- **High** (e.g. impossible travel) -> `user_sessions.suspicious_session`, an
  urgent alert that is **hard-locked against opt-out** (a genuine suspicious-sign-in
  alert is a security control) and bypasses Do-Not-Disturb gates.
- **Medium** (e.g. new country / new device) -> `user_sessions.new_session_review`,
  a reassuring informational message the user may opt out of.

Ships embedded HTML templates in **English and French** (overridable at runtime
through the `Granit.Templating` admin API). Recipient contact is resolved
host-side via `IRecipientResolver`, so this package does **not** depend on
`Granit.Identity` and works for local and federated identities alike.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.UserSessions.Notifications
```

## Dependencies

- `Granit.Notifications`
- `Granit.Templating`
- `Granit.UserSessions.Abstractions`

## Documentation

See the [full documentation](https://granit-fx.dev).
