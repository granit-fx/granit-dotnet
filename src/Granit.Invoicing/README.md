# Granit.Invoicing

Agnostic invoicing module for Granit. Invoice FSM (Draft/Open/Paid/Void/Uncollectible), credit notes, tax calculation via ITaxCalculator, external accounting sync via IInvoiceSyncProvider, PDF generation via IInvoiceDocumentGenerator. Accepts line items from any source (subscriptions, usage, commerce).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Invoicing
```

## Dependencies

- `Granit`
- `Granit.Guids`
- `Granit.Timing`
- `Granit.Workflow`

## Documentation

See the [full documentation](https://granit-fx.dev).
