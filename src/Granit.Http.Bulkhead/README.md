# Granit.Http.Bulkhead

ASP.NET Core binding for [Granit.Bulkhead](../Granit.Bulkhead/README.md).

- `.RequireGranitBulkhead("policy")` — per-endpoint filter limiting concurrent
  requests per tenant (503 Service Unavailable when the bulkhead is saturated).
- RFC 7807 mapping of `BulkheadRejectedException` to HTTP 503.

Policies are configured in the `Bulkhead:Policies` section (owned by the core
`Granit.Bulkhead` package).

## Dependencies

- `Granit.Bulkhead`
- `Granit.Http.ExceptionHandling`

## Documentation

See the [full documentation](https://granit-fx.dev).
