# Granit.CustomerBalance

Per-tenant, per-currency credit balance with append-only ledger for the Granit framework.

## Features

- **Append-only ledger** with immutable `BalanceTransaction` entries (ISO 27001 audit trail)
- **Cached balance** with `IConcurrencyAware` for optimistic concurrency control
- **Multi-tenant, multi-currency** isolation (`BalanceAccount` per tenant + currency)
- **Integration events** for billing orchestration (`BalanceCreditedEto`, `BalanceDebitedEto`, `CreditExpiredEto`)
- **Generic references** via `ReferenceId`/`ReferenceType` (not coupled to Invoicing)

> **Not a payment method or e-money system.** Manages accounting credits only.

## Quick start

```csharp
builder.AddGranitCustomerBalance();
builder.AddGranitCustomerBalanceEntityFrameworkCore(options => ...);
```

## Related packages

| Package | Description |
| ------- | ----------- |
| `Granit.CustomerBalance.EntityFrameworkCore` | EF Core persistence |
| `Granit.CustomerBalance.Endpoints` | REST API for balance and transactions |
| `Granit.CustomerBalance.Wolverine` | Billing choreography integration |
| `Granit.CustomerBalance.BackgroundJobs` | Credit expiration scan |
