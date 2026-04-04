# Granit.Identity.Local.Notifications

Out-of-the-box email and in-app notifications for local identity management.
Provides 9 standard security notification types with Wolverine event handlers,
Scriban templates (18 cultures), and RFC 8058 List-Unsubscribe support.

Part of the [granit](https://granit-fx.dev) framework.

## Notification types

| Notification | Channels | Opt-out | Trigger |
|---|---|---|---|
| `Security.Welcome` | Email | Yes | User registration |
| `Security.PasswordReset` | Email | No | Forgot password flow |
| `Security.EmailConfirmation` | Email | No | Registration / resend |
| `Security.PasswordChanged` | Email | No | Password change (compromise alert) |
| `Security.AccountLocked` | Email, InApp | No | Failed login attempts |
| `Security.TwoFactorChanged` | Email | No | 2FA enabled/disabled |
| `Security.EmailChangeAlert` | Email | No | Email change alert (old address) |
| `Security.EmailChangeConfirmation` | Email | No | Email change confirm (new address) |
| `Security.ImpersonationAlert` | Email, InApp | No | Admin impersonation (GDPR/SOC2) |

## Installation

```bash
dotnet add package Granit.Identity.Local.Notifications
```

## Configuration

```json
{
  "Identity": {
    "Notifications": {
      "FrontendBaseUrl": "https://app.example.com",
      "ResetPasswordPath": "reset-password",
      "ConfirmEmailPath": "confirm-email",
      "ChangeEmailPath": "confirm-email-change"
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
