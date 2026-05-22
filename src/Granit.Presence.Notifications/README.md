# Granit.Presence.Notifications

Soft-dependency bridge between `Granit.Presence` and `Granit.Notifications`. Registers an
`INotificationDeliveryGate` that suppresses push channels for users in `DoNotDisturb` or
`Offline`. Store-and-forward channels keep delivering.

## Suppressed channels

`SignalR`, `Sse`, `WebPush`, `MobilePush`.

## Bypass for critical alerts

A notification type whose `NotificationDefinition.AllowDoNotDisturbBypass` is `true`
skips the gate entirely — reserved for security-critical alerts (suspicious login, MFA
disabled, breach notices).

## Registration

Load the `GranitPresenceNotificationsModule`. No other configuration is needed; the gate
hooks itself into the notification fanout via the standard
`INotificationDeliveryGate` extension point.
