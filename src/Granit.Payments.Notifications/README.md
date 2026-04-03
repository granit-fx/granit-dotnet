# Granit.Payments.Notifications

Notification types for Granit.Payments events.

## Notification types

| Type | Name | Severity | Opt-out |
| ---- | ---- | -------- | ------- |
| `PaymentSucceededNotificationType` | `Payments.PaymentSucceeded` | Success | Yes |
| `PaymentFailedNotificationType` | `Payments.PaymentFailed` | Warning | No |
| `PaymentMethodExpiringNotificationType` | `Payments.PaymentMethodExpiring` | Warning | Yes |
| `RefundProcessedNotificationType` | `Payments.RefundProcessed` | Success | Yes |
| `DisputeOpenedNotificationType` | `Payments.DisputeOpened` | Error | No |
