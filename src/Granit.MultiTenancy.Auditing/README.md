# Granit.MultiTenancy.Auditing

Glue package wiring `Granit.MultiTenancy`'s `IHostImpersonationAuditWriter` to `Granit.Auditing`'s `IAuditingWriter`. Persists host-impersonation attempts (allowed and denied) as ISO 27001 A.12.4 `AccessDenied` / `DataMutation` audit entries.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.MultiTenancy.Auditing
```

## Dependencies

- `Granit.Auditing`
- `Granit.MultiTenancy`
- `Granit.Timing`

## Documentation

See the [full documentation](https://granit-fx.dev).
