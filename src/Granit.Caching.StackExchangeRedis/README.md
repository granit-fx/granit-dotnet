# Granit.Caching.StackExchangeRedis

Redis implementation for Granit.Caching using StackExchange.Redis.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Caching.StackExchangeRedis
```

## Dependencies

- `Granit.Caching`

## Usage

This module upgrades the base `GranitCachingModule` (a prerequisite, pulled in
automatically) with a Redis L2 distributed cache and a Redis pub/sub backplane
for cross-pod L1 invalidation. Declare it on your root host module:

```csharp
[DependsOn(typeof(GranitCachingStackExchangeRedisModule))]
public sealed class MyAppModule : GranitModule { }
```

Optionally register a health check from your host module's `ConfigureServices`:

```csharp
context.Services.AddGranitRedisHealthCheck();
```

## Configuration

Bind the `Cache:Redis` section in `appsettings.json`:

```json
{
  "Cache": {
    "Redis": {
      "IsEnabled": true,
      "ConnectionStringName": "cache",
      "Configuration": "localhost:6379",
      "InstanceName": "dd:"
    }
  }
}
```

`IsEnabled` defaults to `true`. `ConnectionStringName` defaults to `cache`
(resolved from `ConnectionStrings`); when it is null or unresolved, the explicit
`Configuration` connection string is used. `InstanceName` defaults to `dd:`.

## Documentation

See the [full documentation](https://granit-fx.dev).
