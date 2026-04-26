# Granit.Contacts.MultiTenancy

Multi-tenancy integration for [Granit.Contacts](../Granit.Contacts/README.md).
Subscribes to `Granit.MultiTenancy.Events.TenantCreatedEvent` and seeds the
host-scoped `Contact` representing the new tenant via `IDefaultContactSeeder`,
so downstream modules — Invoicing, Subscriptions, Payments, Tax, CustomerBalance —
can resolve a billing identity for the tenant immediately, without manual admin
intervention. Idempotent: Wolverine's at-least-once delivery is safe.

Kept separate from the base `Granit.Contacts` so apps that do not load
`Granit.MultiTenancy` do not inherit a dependency on it.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Contacts.MultiTenancy
```

## Dependencies

- `Granit.Contacts`
- `Granit.MultiTenancy`

## Documentation

See the [full documentation](https://granit-fx.dev).
