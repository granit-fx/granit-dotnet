# Granit.Identity.Local.Privacy

Wolverine handlers for `Granit.Identity.Local`. Ships the `IdentityLocalPrivacyDataProvider`
and a one-line `PersonalDataRequestedEto` handler that forwards to `PrivacyFragmentUploader` —
drop-in GDPR Art. 15/20 export for the built-in local identity module.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Identity.Local.Privacy
```

## Dependencies

- `Granit.Identity.Local`
- `Granit.Privacy.BlobStorage`

## Usage

```csharp
[DependsOn(typeof(GranitIdentityLocalPrivacyModule))]
public class MyAppModule : GranitModule { }
```

Then opt-in on the privacy builder:

```csharp
services.AddGranitPrivacy(privacy => privacy
    .AddGranitIdentityLocalPrivacyProvider());
```

## Fragment contents

The provider exports a single `identity-local.json` fragment containing the user's profile:

- `Id`, `UserName`, `Email`, `EmailConfirmed`
- `FirstName`, `LastName`, `PhoneNumber`, `PhoneNumberConfirmed`
- `TwoFactorEnabled`, `LockoutEnabled`, `LockoutEnd`, `AccessFailedCount`
- `TenantId`, audit fields (`CreatedAt`, `ModifiedBy`, …)
- `IsDeleted`, `DeletedAt`
- `CustomAttributesJson` (extra properties)
- `Roles` (flat list of role names)

If the user has been hard-deleted, the provider returns an empty fragment and the
archive assembler records it under `manifest.EmptyProviders`.

## Documentation

See the [full documentation](https://granit-fx.dev).
