# Granit.Contacts.Privacy

Privacy integration for [Granit.Contacts](../Granit.Contacts/README.md). Ships the
`ContactsPrivacyDataProvider` that exports the user-linked contact (name, emails,
phones, addresses, external mappings) for GDPR Article 15 / 20 requests, plus a
Wolverine-discovered handler that pseudonymises the contact on Article 17 erasure
while preserving the row for accounting integrity (ISO 27001 retention).

Kept separate from the base `Granit.Contacts` module so apps that do not load the
privacy stack do not inherit Privacy + BlobStorage dependencies.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Contacts.Privacy
```

## Dependencies

- `Granit.Contacts`
- `Granit.Privacy.BlobStorage`

## Usage

```csharp
services.AddGranitPrivacy(p => p.AddGranitContactsPrivacyProvider());
```

## Documentation

See the [full documentation](https://granit-fx.dev).
