# Granit.AuditLog.Wolverine

Wolverine integration for the Granit audit trail module.
Replaces no-op event publishers with `IMessageBus`-backed implementations
and provides handlers that persist Settings and Feature Flags changes as
audit log entries with category `ConfigurationChange`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AuditLog.Wolverine
```

## Dependencies

- `Granit.AuditLog`
- `Granit.Settings`
- `Granit.Features`
- `Granit.Wolverine`

## Documentation

See the [full documentation](https://granit-fx.dev).
