# Granit.Http.OutputCaching

Multi-tenant HTTP response caching with GDPR-safe defaults. Wraps ASP.NET Core OutputCaching
with tenant-aware cache isolation, tag-based eviction, and secure-by-default policies that
exclude authenticated responses.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.OutputCaching
```

## Dependencies

- `Granit`

## Quick start

Two steps are required:

1. **Register the module** (services are auto-registered by its `ConfigureServices`):

   ```csharp
   [DependsOn(typeof(GranitHttpOutputCachingModule))]
   public sealed class AppHostModule : GranitModule { }
   ```

2. **Wire the middleware** in `Program.cs`. Ordering is load-bearing: it must run
   after routing/CORS, **after** authentication, and **after** the multi-tenancy
   middleware so the tenant id enters the cache key (cross-tenant disclosure risk
   otherwise), and before authorization:

   ```csharp
   app.UseRouting();
   app.UseCors();
   app.UseGranitMultiTenancy();
   app.UseAuthentication();
   app.UseGranitOutputCaching();   // after tenant + auth
   app.UseAuthorization();
   ```

Configure cache-key variance under the `Http:OutputCaching` section:

```json
{ "Http": { "OutputCaching": { "VaryByQueryKeys": [ "page", "culture" ] } } }
```

The in-memory store is the default; add `Granit.Http.OutputCaching.StackExchangeRedis`
for distributed caching.

## Documentation

See the [full documentation](https://granit-fx.dev).
