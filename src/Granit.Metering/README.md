# Granit.Metering

Usage-based metering for the Granit framework.

## Features

- **High-throughput event recording** with idempotency-key deduplication
- **Pre-computed aggregation** rollups (hourly, daily, billing-period) via watermark pattern
- **Quota enforcement** with configurable threshold alerts (80%/100%)
- **Integration events** for billing orchestration (`UsageSummaryReadyEto`)
- **Multi-tenant** aware with per-tenant meter definitions and usage tracking

## Quick start

```csharp
builder.AddGranitMetering();
builder.AddGranitMeteringEntityFrameworkCore(options => ...);
```

## Related packages

| Package | Description |
| ------- | ----------- |
| `Granit.Metering.EntityFrameworkCore` | EF Core persistence |
| `Granit.Metering.Endpoints` | REST API for usage and quotas |
| `Granit.Metering.BackgroundJobs` | Aggregation and quota check jobs |
| `Granit.Metering.Wolverine` | Event handlers |
