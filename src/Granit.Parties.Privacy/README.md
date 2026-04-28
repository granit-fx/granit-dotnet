# Granit.Parties.Privacy

Privacy integration for [Granit.Parties](../Granit.Parties/README.md). Ships the
`PartiesPrivacyDataProvider` that exports the user-linked contact (name, emails,
phones, addresses, external mappings) for GDPR Article 15 / 20 requests, plus a
Wolverine-discovered handler that pseudonymises the contact on Article 17 erasure
while preserving the row for accounting integrity (ISO 27001 retention).

Kept separate from the base `Granit.Parties` module so apps that do not load the
privacy stack do not inherit Privacy + BlobStorage dependencies.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Parties.Privacy
```

## Dependencies

- `Granit.Parties`
- `Granit.Privacy.BlobStorage`

## Usage

```csharp
services.AddGranitPrivacy(p => p.AddGranitPartiesPrivacyProvider());
```

## Documentation

See the [full documentation](https://granit-fx.dev).
