# Granit.Geocoding

Forward-geocoding engine for `granit`: turn a `PostalAddress` into a `GeoCoordinate`
through a pluggable, cached provider chain.

Part of the [granit](https://granit-fx.dev) framework.

## How it works

`IGeocodingService` orders the registered `IGeocodingProvider`s per
`Geocoding:ProviderOrder`, normalises the address into a stable cache key, and
returns the first provider match. Results are cached through `Granit.Caching`
(`IFusionCache`) with **separate TTLs**: a long one for hits (addresses map to
stable coordinates) and a short one for misses (so a transient failure is retried
soon).

With no provider package installed it is a privacy-first no-op: every address
resolves to `null` without ever throwing or calling out.

```csharp
GeocodingResult? result = await geocoder.GeocodeAsync(
    new PostalAddress(Street: "Rue de la Loi 16", PostalCode: "1000",
                      Locality: "Brussels", Country: "BE"));

GeoCoordinate? point = result?.Coordinate; // null when not geocodable
```

## Registration

```csharp
builder.Services.AddGranitModule<GranitGeocodingModule>(); // via AddGranit module discovery
builder.AddGranitGeocodingNominatim();                     // a provider package
```

```json
{
  "Geocoding": {
    "ProviderOrder": [ "Nominatim" ],
    "SuccessCacheDuration": "90.00:00:00",
    "FailureCacheDuration": "1.00:00:00"
  }
}
```

| Key | Default | Description |
| --- | ------- | ----------- |
| `ProviderOrder` | *(all, registration order)* | Ordered allow-list of provider names |
| `SuccessCacheDuration` | `90.00:00:00` | TTL for a cached successful geocode |
| `FailureCacheDuration` | `1.00:00:00` | TTL for a cached negative result |

## Privacy (GDPR)

A postal address is personal data. The engine **never** writes the address value
to a log, a trace tag, or a cache key:

- Cache keys hash the normalised address (SHA-256), so no raw address lands in a
  shared cache (e.g. Redis), where keys — unlike values — are not encrypted.
- Trace spans carry only the coarse result (`found` / `not_found`).
- A provider failure logs the provider name and exception only.

## Caching & cost

`GetOrSetAsync` coalesces concurrent lookups of the same address to a single
provider call (cache-stampede protection), and an installed distributed cache makes
results cluster-shared — one provider call per address per cluster. List your
cheapest/most-trusted provider first in `ProviderOrder`.

## Dependencies

- `Granit.Geocoding.Abstractions` (the contract)
- `Granit.Caching` (`IFusionCache`)

## See also

- [`Granit.Geocoding.Nominatim`](../Granit.Geocoding.Nominatim/README.md) — the
  OpenStreetMap Nominatim provider.

## License

Apache-2.0
