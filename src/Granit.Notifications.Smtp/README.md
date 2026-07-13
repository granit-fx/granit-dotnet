# Granit.Notifications.Smtp

MailKit SMTP provider for `Granit.Notifications.Email`. Registered as Keyed Service with key `"Smtp"` for multi-provider resolution.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.Smtp
```

## Dependencies

- `Granit.Notifications.Email`

## Configuration

`AddGranitNotificationsSmtp()` binds the `Notifications:Smtp` section
(`SmtpOptions.SectionName`) and validates on start (`.ValidateOnStart()`), so an
invalid `Host` fails fast. Required: `Host` (SMTP server). Optional: `Port`
(default 587), `UseSsl` (default true), `Username`, `Password`,
`DefaultSenderEmail`/`DefaultSenderName`, `TimeoutSeconds` (default 30).

```json
{
  "Notifications": {
    "Email": {
      "Smtp": {
        "Host": "smtp.example.com",
        "Port": 587,
        "UseSsl": true,
        "Username": "",
        "Password": ""
      }
    }
  }
}
```

Inject `Password` via environment (`Notifications__Email__Smtp__Password`)
rather than committing it to `appsettings.json`.

## Documentation

See the [full documentation](https://granit-fx.dev).
