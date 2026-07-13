# Granit.Notifications.WebPush.Privacy

GDPR Art. 15 export and Art. 17 erasure for browser Web Push subscriptions.

- **Export (Art. 15)**: `WebPushPrivacyDataProvider` emits a JSON fragment with **masked** identifiers —
  push credentials never leave in plaintext, and Web Push key material is never exported.
- **Erasure (Art. 17)**: `WebPushPersonalDataDeletionHandler` (Wolverine) hard-deletes every identifier owned by the
  data subject and acknowledges the deletion saga with the **real** deleted row count.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.WebPush.Privacy
```

## Usage

```csharp
builder.AddGranitPrivacy(privacy => privacy.AddGranitWebPushPrivacyProvider());
```

## Dependencies

- `Granit.Notifications.WebPush`
- `Granit.Privacy.BlobStorage`

## Documentation

See the [full documentation](https://granit-fx.dev).
