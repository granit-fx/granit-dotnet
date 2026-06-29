# Granit.Http.Idempotency

HTTP idempotency middleware for Granit APIs. Stripe-style Idempotency-Key header, Redis SET NX PX deduplication, and ISO 27001-compliant audit trail.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.Idempotency
```

## Dependencies

- `Granit.Caching`

## Quick start

Two steps are required:

1. **Register the module** — adds `IdempotencyMiddleware` + DI (the module does
   not add middleware to the pipeline by itself):

   ```csharp
   [DependsOn(typeof(GranitHttpIdempotencyModule))]
   public sealed class AppHostModule : GranitModule { }
   ```

2. **Wire the middleware** in `Program.cs`, **after** authentication so
   `ICurrentUserService` / `ICurrentTenant` are populated when it runs:

   ```csharp
   app.UseAuthentication();
   app.UseAuthorization();
   app.UseGranitIdempotency();   // must run after auth
   ```

## Documentation

See the [full documentation](https://granit-fx.dev).
