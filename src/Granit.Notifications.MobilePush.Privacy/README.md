# Granit.Notifications.MobilePush.Privacy

GDPR Art. 15 export and Art. 17 erasure for mobile push device tokens.

- **Export (Art. 15)**: `MobilePushPrivacyDataProvider` emits a JSON fragment with **masked** identifiers —
  push credentials never leave in plaintext, and Web Push key material is never exported.
- **Erasure (Art. 17)**: `MobilePushPersonalDataDeletionHandler` (Wolverine) hard-deletes every identifier owned by the
  data subject and acknowledges the deletion saga with the **real** deleted row count.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.MobilePush.Privacy
```

## Usage

```csharp
builder.AddGranitPrivacy(privacy => privacy.AddGranitMobilePushPrivacyProvider());
```

## Dependencies

- `Granit.Notifications.MobilePush`
- `Granit.Privacy.BlobStorage`

## Documentation

See the [full documentation](https://granit-fx.dev).
