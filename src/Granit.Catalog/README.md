# Granit.Catalog

Shared product catalog for Granit. Hosts the `Product` aggregate root used as the join key between Metering, Subscriptions, and Invoicing — the "thing being sold" — with lifecycle (Draft/Published/Archived) and external provider mappings (Stripe, Avalara, Odoo). MVP scope: Host-owned billing catalog. Designed to grow toward multi-tenant e-commerce inventory in a future phase.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Catalog
```

## Dependencies

- `Granit`
- `Granit.DataExchange.Abstractions`
- `Granit.Guids`
- `Granit.Localization`
- `Granit.QueryEngine.Abstractions`
- `Granit.Workflow`
