# Granit.AuditLog.ConfigurationChanges

Persists Settings and Feature Flags changes as audit log entries with category
`ConfigurationChange`. Registers `ILocalEventHandler` implementations for
`SettingChangedEvent` and `FeatureOverrideChangedEvent`.

Works with any event bus provider — in-process (`Granit.EventBus`) or
Wolverine-backed (`Granit.EventBus.Wolverine`). No Wolverine dependency.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AuditLog.ConfigurationChanges
```

## Dependencies

- `Granit.AuditLog`
- `Granit.Settings`
- `Granit.Features`

## Documentation

See the [full documentation](https://granit-fx.dev).
