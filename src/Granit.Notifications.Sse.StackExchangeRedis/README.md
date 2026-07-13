# Granit.Notifications.Sse.StackExchangeRedis

Redis pub/sub backplane for the Granit.Notifications **SSE** channel: cross-replica
delivery on Kubernetes. Without a backplane, SSE messages only reach connections on the
node that dispatched them — silent partial delivery (the base channel warns loudly).

- **Publish**: every send goes through the Redis channel to all replicas.
- **Deliver**: each node's subscriber delivers to its local connections only.
- **Resilience**: a malformed envelope is logged, counted
  (`granit.notifications.sse.backplane.message_dropped`) and skipped — a poison message
  never kills the listen loop.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.Sse.StackExchangeRedis
```

Requires an `IConnectionMultiplexer` singleton (e.g. from `Granit.Caching.StackExchangeRedis`).

## Dependencies

- `Granit.Notifications.Sse`
- `StackExchange.Redis`

## Documentation

See the [full documentation](https://granit-fx.dev).
