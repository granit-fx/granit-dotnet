# Granit.Identity.Abstractions

Identity session and device contracts for `granit`. Provides `UserSessionDescriptor`
— the canonical session shape produced by both the BFF gateway and the identity
authority — together with `DeviceKind`, the session/device providers, the session
lifecycle events, and the durable `IUserSessionRiskStore`.

Reference this package to consume or enrich user sessions uniformly across session
sources, without depending on a specific session store or the anomaly-detection
engine.

Part of the [granit](https://granit-fx.dev) framework.

## Why a shared contract

The BFF (`Granit.Bff`) and the identity authority (`Granit.Identity`) are two
independent session layers at different trust boundaries. Rather than couple them,
both project to `UserSessionDescriptor`, so a single enrichment pipeline
(geolocation, last-activity) and one risk model serve both — `Bff → Identity ← IdP`.

## What's included

| Type | Purpose |
| ---- | ------- |
| `UserSessionDescriptor` | Canonical, store-agnostic session shape |
| `DeviceKind` / `UserDevice` | Device classification from the authentication context |
| `IUserSessionProvider` / `IUserDeviceProvider` | Backend-agnostic session/device sources |
| `UserSessionCreatedEto` / `SuspiciousUserSessionDetectedEto` | Session lifecycle events |
| `IUserSessionAnomalyDetector` | Evaluates a session against history (no-op by default) |
| `IUserSessionRiskStore` / `UserSessionRiskVerdict` | Durable, restart-safe risk verdicts |

## Defaults

Registering `GranitIdentityAbstractionsModule` wires safe defaults: no-op session and
device providers, a no-op anomaly detector (`UserSessionRiskLevel.None`), and an
in-memory risk store. Install `Granit.UserSessions.AnomalyDetection` to enable
detection and `Granit.UserSessions.EntityFrameworkCore` for a durable,
cluster-shared risk store.

## Installation

```bash
dotnet add package Granit.Identity.Abstractions
```
