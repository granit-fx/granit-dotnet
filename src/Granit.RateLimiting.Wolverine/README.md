# Granit.RateLimiting.Wolverine

Wolverine binding for [`Granit.RateLimiting`](https://granit-fx.dev). A pipeline middleware enforces
the `[RateLimited("policy")]` attribute on message types before the handler runs, throwing
`RateLimitExceededException` (handled by Wolverine's retry policy) when the per-tenant quota is
exceeded.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.RateLimiting.Wolverine
```

## Usage

Decorate the message and register the middleware in your Wolverine setup:

```csharp
[RateLimited("ingest")] // policy under RateLimiting:Policies:ingest
public sealed record IngestDocument(Guid Id);

opts.Policies.AddMiddleware<RateLimitMiddleware>(
    chain => chain.MessageType.GetCustomAttributes(typeof(RateLimitedAttribute), true).Length > 0);
```

`GranitRateLimitingWolverineModule` pulls in the core `GranitRateLimitingModule` automatically.

The middleware is discovered by Wolverine convention and uses no Wolverine types, so the package
carries **no** `WolverineFx` reference — the host's Wolverine setup is sufficient.

## Dependencies

- `Granit.RateLimiting`

## Documentation

See the [full documentation](https://granit-fx.dev).
