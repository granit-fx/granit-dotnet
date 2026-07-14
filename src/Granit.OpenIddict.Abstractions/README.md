# Granit.OpenIddict.Abstractions

Runtime-agnostic OpenIddict contracts for Granit: the signing-key store and rotation
interfaces, the `SigningKey` aggregate, the claims-destination provider, and the
permission/feature/setting name catalogues. Reference this package to consume the OpenIddict
server's contracts without taking a dependency on OpenIddict, EF Core, or ASP.NET Core.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.OpenIddict.Abstractions
```

## Contents

- `ISigningKeyStore`, `IKeyRotationService` — signing-key persistence and rotation contracts
- `SigningKey`, `SigningKeyStatus` — the signing-key aggregate
- `IClaimsDestinationProvider`, `ClaimsDestinations` — claim-to-token routing
- `OpenIddictPermissions`, `OpenIddictFeatureNames`, `OpenIddictSettingNames` — name catalogues

The concrete implementations live in `Granit.OpenIddict` (claims-destination provider,
key-rotation service) and `Granit.OpenIddict.EntityFrameworkCore` (signing-key store).
