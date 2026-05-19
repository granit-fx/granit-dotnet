# Granit.MultiTenancy.Authorization

Glue package wiring `Granit.MultiTenancy`'s `IHostImpersonationGate` to `Granit.Authorization`'s permission system. Adds the `MultiTenancy.Host.Impersonate` permission so Host operators can be explicitly authorized to impersonate a tenant via `X-Tenant-Id` (or any non-JWT resolver).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.MultiTenancy.Authorization
```

## Dependencies

- `Granit.Authorization`
- `Granit.Localization`
- `Granit.MultiTenancy`

## Documentation

See the [full documentation](https://granit-fx.dev).
