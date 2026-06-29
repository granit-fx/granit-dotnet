# Granit.Notifications.Brevo

Unified Brevo (formerly Sendinblue) provider for `Granit.Notifications`.
Implements `IEmailSender`, `ISmsSender`, and `IWhatsAppSender` via Brevo
Transactional API. Single `BrevoNotificationProvider` class registered as
three Keyed Services with key `"Brevo"`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.Brevo
```

## Dependencies

- `Granit.Notifications.Email`
- `Granit.Notifications.Sms`
- `Granit.Notifications.WhatsApp`

## Configuration

`AddGranitNotificationsBrevo()` binds the `Notifications:Brevo` section
(`BrevoOptions.SectionName`) and validates at startup
(`ValidateDataAnnotations` + `ValidateOnStart`) — missing or invalid required
config fails app startup.

Required: `ApiKey` (Brevo account API key — a SECRET, inject via env
`Notifications__Brevo__ApiKey`), `DefaultSenderEmail` (transactional sender
address), `BaseUrl` (default `https://api.brevo.com/v3`). Optional:
`DefaultSenderName`, `DefaultSmsSenderId`, `TimeoutSeconds` (HTTP timeout,
default 30, range 1-300).

```json
{
  "Notifications": {
    "Brevo": {
      "ApiKey": "",
      "DefaultSenderEmail": "noreply@example.com",
      "BaseUrl": "https://api.brevo.com/v3",
      "TimeoutSeconds": 30
    }
  }
}
```

## Documentation

See the [full documentation](https://granit-fx.dev).
