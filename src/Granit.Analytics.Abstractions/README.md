# Granit.Analytics.Abstractions

Inter-module contracts for `Granit.Analytics`. Currently hosts `PeriodSpec` (the
token-or-absolute time window backing `MetricRequest.Period`), shared between
the analytics HTTP layer and the upcoming dashboard time-window primitive
(`DashboardTimeWindow` in `Granit.Dashboards.Abstractions`, P1.3 follow-up).

Reference [Granit.Analytics](../Granit.Analytics/) for the runtime
(`MetricDefinition`, executors, ...). This package stays minimal so consumers
that only need the shared contracts (cross-module DTOs, dashboard layer) don't
pull the analytics runtime.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Analytics.Abstractions
```

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
