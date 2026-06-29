# Granit.Http.ResponseCompression

Standardized HTTP response compression for Granit applications. Brotli (primary)
and gzip (fallback) with safe HTTPS defaults and SSE/WebSocket exclusion.
Configurable via `appsettings.json`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.ResponseCompression
```

## Quick start

Two steps are required:

1. Reference the module on your host module so its services are configured:

   ```csharp
   [DependsOn(typeof(GranitHttpResponseCompressionModule))]
   public sealed class AppHostModule : GranitModule { }
   ```

2. Activate the middleware in `Program.cs`:

   ```csharp
   app.UseGranitResponseCompression();
   ```

   It must be placed very early in the pipeline — before output-caching,
   authorization, and endpoints — so it wraps all downstream response bodies:

   ```csharp
   app.UseGranitResponseCompression();
   app.UseGranitOutputCaching();
   app.UseAuthorization();
   ```

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
