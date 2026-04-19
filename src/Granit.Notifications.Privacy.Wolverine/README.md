# Granit.Notifications.Privacy.Wolverine

Wolverine privacy handlers for `Granit.Notifications`. Ships the
`NotificationsPrivacyDataProvider` and a one-line `PersonalDataRequestedEto`
handler that forwards to `PrivacyFragmentUploader` — drop-in GDPR Art. 15
export of the user's notification data.

Kept separate from `Granit.Notifications.Wolverine` (which targets durable
dispatch) so consumers of that package do not inherit a Privacy + BlobStorage
dependency they may not need.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.Privacy.Wolverine
```

## Dependencies

- `Granit.Notifications`
- `Granit.Privacy.BlobStorage`

## Usage

```csharp
[DependsOn(typeof(GranitNotificationsPrivacyWolverineModule))]
public class MyAppModule : GranitModule { }
```

Opt-in on the privacy builder:

```csharp
services.AddGranitPrivacy(privacy => privacy
    .AddGranitNotificationsPrivacyProvider());
```

## Fragment contents

The provider exports `notifications.json` containing:

- **Inbox** — up to 5 000 `UserNotification` rows for the user (flagged
  `truncated: true` if the limit is hit). Each entry includes the
  notification type, severity, state, related-entity reference, and the
  original `Data` payload.
- **Preferences** — every registered `NotificationPreference` (per type/channel).
- **Subscriptions** — every `NotificationSubscription` the user holds
  (topic + entity-follower subscriptions).

The empty fragment is emitted when the user has no data in any of the three
collections, so the archive assembler records the provider under
`manifest.EmptyProviders` rather than including an empty file.

## Documentation

See the [full documentation](https://granit-fx.dev).
