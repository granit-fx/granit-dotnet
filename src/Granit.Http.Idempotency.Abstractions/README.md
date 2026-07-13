# Granit.Http.Idempotency.Abstractions

Contracts for [Granit.Http.Idempotency](../Granit.Http.Idempotency/README.md):

- `[Idempotent]` — endpoint attribute declaring an idempotent route (optional
  per-endpoint TTL override, `Required` flag).
- `IIdempotencyMetadata` — the marker the middleware reads from endpoint metadata.

Pure POCOs with zero dependencies: `.Endpoints` packages reference this to decorate
routes without pulling in the middleware (or ASP.NET Core through it).

## Documentation

See the [full documentation](https://granit-fx.dev).
