# Granit.Caching.FusionCache

FusionCache provider for Granit.Caching: L1 local memory + L2 Redis + backplane
for cross-pod invalidation + fail-safe + factory timeouts + eager refresh +
native OpenTelemetry.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Caching.FusionCache
```

## Dependencies

- `Granit.Caching.StackExchangeRedis`
- `ZiggyCreatures.FusionCache`

## Documentation

See the [full documentation](https://granit-fx.dev).
