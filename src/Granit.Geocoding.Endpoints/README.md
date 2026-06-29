# Granit.Geocoding.Endpoints

Minimal API endpoints for the geocoding capabilities of `granit`: **address autocomplete**
(typeahead) and **reverse geocoding**.

Part of the [granit](https://granit-fx.dev) framework.

## Endpoints

| Method | Route | Mapped when |
| ------ | ----- | ----------- |
| `GET` | `/{prefix}/autocomplete?q=&limit=` | an `IAddressAutocompleteProvider` is registered |
| `GET` | `/{prefix}/reverse?lat=&lon=` | an `IReverseGeocodingProvider` is registered |

Each endpoint is **capability-gated**: it is mapped only when a capable provider is installed
(`GeocodingCapabilities`), so the API surface reflects what the host actually configured. With
Nominatim only, `/reverse` exists but `/autocomplete` does not; add Photon and `/autocomplete`
appears too.

## Registration

Register the module **before** mapping the endpoints — `MapGranitGeocoding()`
resolves `GeocodingCapabilities` from DI and throws at startup otherwise. The
Granit module loader discovers modules strictly by traversing `[DependsOn]`
from the root (no assembly auto-scan), so declare the dependency on your host
module:

```csharp
[DependsOn(typeof(GranitGeocodingEndpointsModule))]   // transitively pulls GranitGeocodingModule (supplies GeocodingCapabilities)
public sealed class AppHostModule : GranitModule { }
```

## Usage

```csharp
// Host: map under "/geocoding" (default).
app.MapGranitGeocoding()
   .RequireGranitRateLimiting("geocoding");   // see Security below
```

## Security

- **Authenticated by default.** Geocoding proxies an external, rate-limited, billable provider, so the
  group is never mapped anonymous. Override with `GeocodingEndpointsOptions.AuthorizationPolicy`.
- **Apply a per-principal rate-limit policy.** Autocomplete is per-keystroke and the upstream provider
  quota is a *shared* resource — one principal's spam can exhaust the quota / bill for every tenant
  (denial-of-wallet). Chain `.RequireGranitRateLimiting("geocoding")` and configure
  `RateLimiting:Policies:geocoding` partitioned **by user / tenant**:

  ```json
  { "RateLimiting": { "Policies": { "geocoding": {
      "PermitLimit": 60, "Window": "00:01:00", "Algorithm": "SlidingWindow", "PartitionBy": "TenantAndUser" } } } }
  ```

## See also

- [`Granit.Geocoding`](../Granit.Geocoding/README.md) — the engine and the autocomplete / reverse services.
- [`Granit.Geocoding.Abstractions`](../Granit.Geocoding.Abstractions/README.md) — the capability contracts.
