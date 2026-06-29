# Granit.Notifications.MobilePush

Mobile push notification channel for Granit.Notifications. Provides `IMobilePushSender` abstraction, device token store (`IMobilePushTokenReader`/`IMobilePushTokenWriter`), and `MobilePushNotificationChannel` with Keyed Services multi-provider resolution.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.MobilePush
```

## Dependencies

- `Granit.Notifications`

## Production / persistence

`AddGranitNotificationsMobilePush()` registers an in-memory token store
(`InMemoryMobilePushTokenStore`) that is stateless and loses device tokens on
restart — not suitable for production.

For production, add the `Granit.Notifications.EntityFrameworkCore` package and
call `AddGranitNotificationsEntityFrameworkCore()`; it replaces the in-memory
store with `EfCoreMobilePushTokenStore`, persisting tokens in the database.

When at-rest token hashing is enabled, configure
`Notifications:MobilePush:TokenHasher:DeviceTokenLookupPepper` with a
high-entropy secret of at least 32 bytes (e.g. `openssl rand -hex 32`), supplied
via environment/secret store and **not** committed to `appsettings.json` —
otherwise token hashing throws at startup.

## Documentation

See the [full documentation](https://granit-fx.dev).
