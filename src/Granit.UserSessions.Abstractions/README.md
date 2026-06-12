# Granit.UserSessions.Abstractions

Shared session read-model and risk contracts for `granit`. Provides
`SessionDescriptor` — the canonical session shape produced by both the BFF gateway
and the identity authority — together with `ISessionAnomalyDetector` and the
durable `ISessionRiskStore`.

Reference this package to consume or enrich user sessions uniformly across session
sources, without depending on a specific session store or the anomaly-detection
engine.

Part of the [granit](https://granit-fx.dev) framework.

## Why a shared contract

The BFF (`Granit.Bff`) and the identity authority (`Granit.Identity`) are two
independent session layers at different trust boundaries. Rather than couple them,
both project to `SessionDescriptor`, so a single enrichment pipeline (geolocation,
last-activity) and one risk model serve both — `Bff → UserSessions ← Identity`.

## What's included

| Type | Purpose |
| ---- | ------- |
| `SessionDescriptor` | Canonical, store-agnostic session shape |
| `ISessionAnomalyDetector` | Evaluates a session against history (no-op by default) |
| `SessionRiskAssessment` / `SessionRiskLevel` | Anomaly outcome |
| `ISessionRiskStore` / `SessionRiskVerdict` | Durable, restart-safe risk verdicts |

## Defaults

Registering `GranitUserSessionsAbstractionsModule` wires safe defaults: a no-op
anomaly detector (`SessionRiskLevel.None`) and an in-memory risk store. Install
`Granit.UserSessions.AnomalyDetection` to enable detection and
`Granit.UserSessions.EntityFrameworkCore` for a durable, cluster-shared risk store.

## Installation

```bash
dotnet add package Granit.UserSessions.Abstractions
```

## Dependencies

- `Granit`
- `Granit.IpGeolocation` (for the `GeoLocation` field on `SessionDescriptor`)

## Documentation

See the [full documentation](https://granit-fx.dev).
