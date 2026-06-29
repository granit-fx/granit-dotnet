# Granit.Caching

Distributed caching abstractions for Granit applications.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Caching
```

## Dependencies

- `Granit`

## Usage

This module is typically pulled in transitively by other Granit modules
(Localization, BFF, Geocoding, and more). To use it standalone, declare it on
your host module so its `ConfigureServices` runs:

```csharp
[DependsOn(typeof(GranitCachingModule))]
public sealed class MyAppModule : GranitModule { }
```

It registers `IFusionCache` for in-memory L1 caching with fail-safe and
OpenTelemetry support.

## Configuration

Bind the `Cache` section in `appsettings.json`:

```json
{
  "Cache": {
    "KeyPrefix": "dd",
    "DefaultAbsoluteExpirationRelativeToNow": "01:00:00",
    "EncryptValues": false
  }
}
```

`KeyPrefix` defaults to `dd`, `DefaultAbsoluteExpirationRelativeToNow` to one
hour, and `EncryptValues` to `false`. Set `EncryptValues: true` to encrypt
cached values (or opt in per type with `[CacheEncrypted]`).

## Documentation

See the [full documentation](https://granit-fx.dev).
