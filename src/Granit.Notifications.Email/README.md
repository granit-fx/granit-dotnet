# Granit.Notifications.Email

Email notification channel for Granit.Notifications. Provides `IEmailSender` abstraction, `EmailNotificationChannel` with Keyed Services multi-provider resolution, and Scriban template rendering.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.Email
```

## Dependencies

- `Granit.Notifications`

## Configuration

`AddGranitNotificationsEmail()` registers only the channel — it resolves the
`IEmailSender` at send time via keyed services. You **must** also register at
least one provider that supplies a keyed `IEmailSender`:

```csharp
context.Services.AddGranitNotificationsEmail();
context.Services.AddGranitNotificationsEmailSmtp(); // keyed "Smtp" (the default)
// or context.Services.AddGranitNotificationsBrevo(); // keyed "Brevo"
```

Without a provider, `EmailNotificationChannel.SendAsync` throws
(`GetRequiredKeyedService`) at send time.

Channel settings are bound from the `Notifications:Email` section:

```json
{
  "Notifications": {
    "Email": {
      "Provider": "Smtp",
      "DefaultSenderEmail": "noreply@example.com",
      "DefaultSenderName": "Example"
    }
  }
}
```

`Provider` must match a registered keyed provider (defaults to `"Smtp"`).
`DefaultSenderEmail`/`DefaultSenderName`/`UnsubscribeUrl` default to empty and
are only needed for correct sender display, not for channel resolution.

## Documentation

See the [full documentation](https://granit-fx.dev).
