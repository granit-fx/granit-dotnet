# Granit.Presence.Wolverine

Wolverine integration for `Granit.Presence`. Subscribes to user-account
lifecycle events (`AccountDeletedEto`) and purges the corresponding presence
override and cached heartbeat — closes the GDPR Art. 17 (right to erasure) loop
when `Granit.Identity.Local` is loaded alongside `Granit.Presence`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Presence.Wolverine
```

## Dependencies

- `Granit.Identity.Local`
- `Granit.Presence`
- `Granit.Wolverine`

## Documentation

See the [full documentation](https://granit-fx.dev).
