# Granit.Tax

Tax calculation and validation abstractions for the Granit framework.

## Features

- **ITaxIdValidator** — online VAT number validation (VIES, Stripe Tax, HMRC)
- **ITaxRateProvider** — tax rate lookup by country and date
- **EU country registry** — 27 member states with EL/GR mapping, excludes GB/XI
- **Tax exemption reasons** — ReverseCharge, Export, IntraCommunitarySupply
- **Cached validation results** — ValidatedTaxId entity with configurable TTL

## Providers

| Package | Strategy | Use case |
| ------- | -------- | -------- |
| `Granit.Tax.Internal` | Self-hosted EU VAT | B2B SaaS, 1-2 countries, sovereignty |
| `Granit.Tax.Stripe` | Stripe Tax API | B2C global, US Sales Tax, multi-jurisdiction |

## Quick start

```csharp
builder.AddGranitTax();
builder.AddGranitTaxInternal(); // or builder.AddGranitTaxStripe();
```
