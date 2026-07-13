# Granit.Bulkhead.Wolverine

Wolverine binding for [Granit.Bulkhead](../Granit.Bulkhead/README.md).

Pipeline middleware enforcing the `[Bulkhead("policy")]` attribute on message types
before the handler runs, throwing `BulkheadRejectedException` (handled by the
Wolverine retry policy) when the per-tenant concurrency limit is saturated.

```csharp
opts.Policies.AddMiddleware<BulkheadMiddleware>(
    chain => chain.MessageType.GetCustomAttributes(typeof(BulkheadAttribute), true).Length > 0);
```

No `WolverineFx` reference: the middleware is discovered by convention and uses no
Wolverine types — the host's Wolverine setup is sufficient.

## Documentation

See the [full documentation](https://granit-fx.dev).
