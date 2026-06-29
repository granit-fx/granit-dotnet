# Granit.Webhooks

Outgoing webhook engine for Granit. In-process channel-based asynchronous dispatch by default (bounded channels + background worker); HMAC-SHA256 anti-replay signature, durable exponential backoff. For durable outbox-backed dispatch, add the `Granit.Webhooks.Wolverine` package and wire `GranitWebhooksWolverineModule`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Webhooks
```

## Dependencies

- `Granit`
- `Granit.Guids`
- `Granit.Timing`

## Documentation

See the [full documentation](https://granit-fx.dev).
