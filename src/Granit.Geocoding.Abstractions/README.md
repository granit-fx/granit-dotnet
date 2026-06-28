# Granit.Geocoding.Abstractions

Source-agnostic forward-geocoding contract for `granit`: turn a `PostalAddress`
into a `GeoPoint` (latitude/longitude), **without** taking a runtime dependency on
the `Granit.Geocoding` engine or any concrete provider.

Part of the [granit](https://granit-fx.dev) framework.

## Why a separate package

A consumer that only needs the *contract* — to inject `IGeocodingService`, accept a
`PostalAddress`, store a `GeoPoint` — should not pull in the engine, its provider
chain, or its caching stack. This package holds the contract alone, so the
implementation stays an optional, swappable concern.

## The contract

```csharp
public interface IGeocodingService
{
    // Returns null when not geocodable — never throws.
    Task<GeoPoint?> GeocodeAsync(PostalAddress address, CancellationToken ct = default);
}

public interface IGeocodingProvider          // low-level, one per provider package
{
    string ProviderName { get; }
    Task<GeoPoint?> ResolveAsync(PostalAddress address, CancellationToken ct = default);
}

public sealed record PostalAddress(string? Street, string? PostalCode, string Locality, string Country);
public sealed record GeoPoint(double Latitude, double Longitude);
```

## Why `PostalAddress` and not `Granit.Domain.ValueObjects.Address`

`Address` is a domain value object whose invariants are tuned for a *deliverable
mailing address* — it requires a non-empty street line and postal code. Those
invariants are too strict for geocoding: a geocoder happily resolves a coordinate
from just a locality and a country (`"Brussels", "BE"`) with no street at all.

`PostalAddress` therefore keeps a looser shape — only `Locality` and `Country` are
required — so the geocoding contract never has to relax the domain `Address`
invariants, and the two concerns evolve independently.

## See also

- [`Granit.Geocoding`](../Granit.Geocoding/README.md) — the engine that produces
  `GeoPoint` through a pluggable, cached provider chain.
- [`Granit.Geocoding.Nominatim`](../Granit.Geocoding.Nominatim/README.md) — the
  OpenStreetMap Nominatim provider.
