# Granit.Parties.MultiTenancy

Multi-tenancy integration for [Granit.Parties](../Granit.Parties/README.md).
Subscribes to `Granit.MultiTenancy.Events.TenantCreatedEvent` and seeds the
host-scoped `Party` representing the new tenant via `IDefaultPartySeeder`,
so downstream modules — Invoicing, Subscriptions, Payments, Tax, CustomerBalance —
can resolve a billing identity for the tenant immediately, without manual admin
intervention. Idempotent: Wolverine's at-least-once delivery is safe.

Kept separate from the base `Granit.Parties` so apps that do not load
`Granit.MultiTenancy` do not inherit a dependency on it.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Parties.MultiTenancy
```

## Dependencies

- `Granit.Parties`
- `Granit.MultiTenancy`

## Documentation

See the [full documentation](https://granit-fx.dev).
