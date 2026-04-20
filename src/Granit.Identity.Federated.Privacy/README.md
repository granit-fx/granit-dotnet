# Granit.Identity.Federated.Privacy

Wolverine handlers for `Granit.Identity.Federated`. Ships the
`IdentityFederatedPrivacyDataProvider` and a one-line `PersonalDataRequestedEto`
handler that forwards to `PrivacyFragmentUploader` — drop-in GDPR Art. 15
export of the local federated identity cache entry.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Identity.Federated.Privacy
```

## Dependencies

- `Granit.Identity.Federated`
- `Granit.Privacy.BlobStorage`

## Usage

```csharp
[DependsOn(typeof(GranitIdentityFederatedPrivacyModule))]
public class MyAppModule : GranitModule { }
```

Opt-in on the privacy builder:

```csharp
services.AddGranitPrivacy(privacy => privacy
    .AddGranitIdentityFederatedPrivacyProvider());
```

## Fragment contents

The provider exports `identity-federated.json` containing the local cache mirror:

- `cacheEntryId`, `externalUserId`, `username`, `email`
- `firstName`, `lastName`, `enabled`
- `lastSyncedAt`, `tenantId`
- audit fields (`createdAt`, `modifiedAt`)
- `extraPropertiesJson` (provider-specific attributes)

The provider returns an empty fragment when the user authenticated at least
once but has no cache entry (e.g. they never triggered a feature that
populates the cache) — the archive assembler then records the provider under
`manifest.EmptyProviders`.

## Documentation

See the [full documentation](https://granit-fx.dev).
