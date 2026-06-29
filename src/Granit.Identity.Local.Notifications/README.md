# Granit.Identity.Local.Notifications

Out-of-the-box email and in-app notifications for local identity management.
Provides 9 standard security notification types with Wolverine event handlers,
Scriban templates (18 cultures), and RFC 8058 List-Unsubscribe support.

Part of the [granit](https://granit-fx.dev) framework.

## Notification types

| Notification | Channels | Opt-out | Trigger |
| --- | --- | --- | --- |
| `identity.welcome` | Email | Yes | User registration |
| `identity.password_reset` | Email | No | Forgot password flow |
| `identity.email_confirmation` | Email | No | Registration / resend |
| `identity.password_changed` | Email | No | Password change (compromise alert) |
| `identity.account_locked` | Email, InApp | No | Failed login attempts |
| `identity.two_factor_changed` | Email | No | 2FA enabled/disabled |
| `identity.email_change_alert` | Email | No | Email change alert (old address) |
| `identity.email_change_confirmation` | Email | No | Email change confirm (new address) |
| `identity.impersonation_alert` | Email, InApp | No | Admin impersonation (GDPR/SOC2) |

## Installation

```bash
dotnet add package Granit.Identity.Local.Notifications
```

## Configuration

```json
{
  "Identity": {
    "Local": {
      "Notifications": {
        "FrontendBaseUrl": "https://app.example.com",
        "ResetPasswordPath": "reset-password",
        "ConfirmEmailPath": "confirm-email",
        "ChangeEmailPath": "confirm-email-change"
      }
    }
  }
}
```

## Dependencies

- `Granit.Identity.Local`
- `Granit.Identity`
- `Granit.Notifications.Abstractions`
- `Granit.Templating`

## Documentation

See the [full documentation](https://granit-fx.dev).
