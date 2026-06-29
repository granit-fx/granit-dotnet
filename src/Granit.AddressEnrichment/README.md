# Granit.AddressEnrichment

Address enrichment orchestrator for `granit`: turn a domain `Address` into its
**geocoding outcome** (`AddressGeocoding`) and **deliverability verdict**
(`AddressVerification`) in one call, by composing the geocoding engine (tier 0) and an
optional address-verification provider (tier 1).

Part of the [granit](https://granit-fx.dev) framework.

## Why a thin orchestrator

Every address-holding entity needs the same two derived axes materialized the same way.
Rather than each module re-implementing "geocode, map to a status, optionally verify",
this package centralizes the mapping behind one contract:

```csharp
public interface IAddressEnrichmentService
{
    Task<AddressEnrichmentResult> EnrichAsync(Address address, CancellationToken ct = default);
}

public sealed record AddressEnrichmentResult(AddressGeocoding Geocoding, AddressVerification Verification);
```

It sits **above** `Granit.Geocoding` and `Granit.AddressDeliverability.Abstractions` (it
never makes the geocoding engine depend on deliverability).

## Behaviour

- **Tier 0 (always):** geocodes via `IGeocodingService`; a hit becomes
  `AddressGeocoding.Resolved` (or `Approximate` for a locality centroid), a miss becomes
  `AddressGeocoding.Failed`.
- **Tier 1 (optional):** if an `IAddressDeliverabilityService` provider is registered, it is
  called and the provider outcome is mapped onto `AddressVerificationStatus`
  (`Verified → ProviderVerified`, `Corrected → Corrected`, `Invalid → Invalid`,
  `Unverifiable → Unverified`). With no provider, the verdict stays `Unverified`.

## Registration

`GranitAddressEnrichmentModule` `[DependsOn]` `GranitGeocodingModule`, so a geocoding
service is always present (the privacy-first no-op default when no provider is installed).
The verification provider is an optional soft dependency, resolved at registration time.

## See also

- [`Granit.Geocoding`](../Granit.Geocoding/README.md) — tier 0 engine.
- [`Granit.AddressDeliverability.Abstractions`](../Granit.AddressDeliverability.Abstractions/README.md) — tier 1 contract.
