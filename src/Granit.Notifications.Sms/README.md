# Granit.Notifications.Sms

SMS notification channel for Granit.Notifications. Provides `ISmsSender` abstraction and `SmsNotificationChannel` with Keyed Services multi-provider resolution.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.Sms
```

## Dependencies

- `Granit.Notifications`

## Configuration

The SMS channel is abstract. `AddGranitNotificationsSms()` registers only the
channel and resolves the `ISmsSender` at send time via keyed services
(`SmsChannelOptions.Provider`, section `Notifications:Sms`). You **must** also
register at least one provider that supplies a keyed `ISmsSender`:

```csharp
context.Services.AddGranitNotificationsSms();
context.Services.AddGranitNotificationsBrevo(); // keyed "Brevo"
```

Without a provider, `SmsNotificationChannel.SendAsync` throws
(`GetRequiredKeyedService`) at send time.

## Documentation

See the [full documentation](https://granit-fx.dev).
