# Granit.AddressDeliverability.Abstractions

Address-deliverability (authoritative verification) contract for `granit`: check a domain
`Address` against an authoritative source and get back an outcome, a standardized address,
provider flags and a match code — **without** taking a dependency on any concrete provider.

Part of the [granit](https://granit-fx.dev) framework.

## Where it fits — the three evidence tiers

An address has two independent axes: *can we place it on a map?* (geocoding) and *is it
real / deliverable?* (verification). The verification verdict
(`Granit.Domain.ValueObjects.AddressVerification`) is fed by three evidence tiers:

| Tier | Source | This package |
| ---- | ------ | ------------ |
| 0 | Geocoding plausibility (OSM existence + match granularity) | `Granit.Geocoding` |
| 1 | **Authoritative provider** (DPV / RDI — Smarty, Loqate, Google Address Validation) | **this contract** |
| 2 | Real-world evidence (manual confirmation, successful delivery / mail) | consuming module |

This package holds **tier 1** as a contract only. No concrete provider ships in the
framework baseline; a host opts in by registering a provider package.

## The contract

```csharp
public interface IAddressDeliverabilityService
{
    Task<AddressDeliverabilityResult> CheckAsync(Address address, CancellationToken ct = default);
}

public sealed record AddressDeliverabilityResult(
    AddressDeliverabilityOutcome Outcome,        // Verified | Corrected | Unverifiable | Invalid
    Address? Standardized = null,                // the provider's corrected address
    IReadOnlyList<string>? Flags = null,         // DPV / residential / vacant / …
    string? ProviderMatchCode = null);
```

The orchestrator (`Granit.AddressEnrichment`) maps the provider `Outcome` onto the
persisted `Granit.Domain.ValueObjects.AddressVerificationStatus`.

## Why "Deliverability" and not "Verification"

The persisted verdict value object is `AddressVerification`. Naming this package
`Granit.AddressVerification` would create a namespace that shadows that type in every
consumer. "Deliverability" names the *provider check* (a delivery-point validation),
distinct from the recorded *verification verdict* it contributes to — and keeps the two
free of any namespace/type clash.

## Why not `Granit.Validation`

`Granit.Validation*` is the FluentValidation-based **input-validation** family (is a
request well-formed?). Deliverability answers a different question — *does this address
physically exist and receive mail?* — against an external authority.

## See also

- [`Granit.AddressEnrichment`](../Granit.AddressEnrichment/README.md) — composes geocoding
  (tier 0) and this contract (tier 1) into the framework address value objects.
