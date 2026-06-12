# Granit.IpGeolocation.Abstractions

Source-agnostic geographic contract for `granit`: the `GeoLocation` read-model
(city, region, country, coordinates) produced by IP geolocation and consumable
by any module — e.g. user-session enrichment — **without** taking a runtime
dependency on the `Granit.IpGeolocation` resolver engine.

Part of the [granit](https://granit-fx.dev) framework.

## Why a separate package

A consumer that only needs to *read* a resolved location (store it, display it,
risk-score it) should not pull in the resolver, its provider chain, or its
caching stack. This package holds the contract alone, so a module like
`Granit.UserSessions` can depend on the shape while the resolver implementation
stays an optional, swappable concern.

The shape is deliberately not IP-specific — a city/region/country is a location
regardless of how it was resolved — so a future non-IP geolocation module
(e.g. address geocoding) can reuse the same contract.

## The contract

```csharp
public sealed record GeoLocation
{
    public string? City { get; init; }         // "Brussels"
    public string? Region { get; init; }       // "Brussels-Capital"
    public string? Country { get; init; }      // "Belgium"
    public string? CountryCode { get; init; }  // ISO 3166-1 alpha-2, "BE"
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
}
```

Every member is optional: a provider populates only what its data source
supports. An offline country-only database leaves `City` and the coordinates
`null`; a city-grade source fills them in.

## See also

- [`Granit.IpGeolocation`](../Granit.IpGeolocation/README.md) — the resolver that
  produces `GeoLocation` through a pluggable, cached provider chain.
