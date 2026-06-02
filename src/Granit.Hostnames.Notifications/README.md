# Granit.Hostnames.Notifications

Email notifications for `Granit.Hostnames` DNS verification events.

## What it does

Listens to two integration events dispatched by `ManagedHostname` and sends templated emails
to the hostname owner (`OwnerId`):

| Event | Notification type |
| ----- | ----------------- |
| `HostnameVerifiedEto` | `hostnames.hostname_verified` |
| `HostnameVerificationFailedEto` | `hostnames.hostname_verification_failed` |

Templates ship in all 18 Granit cultures (EN + FR hand-authored; 16 auto-translated).
Users may opt out via the standard notification preference system (`AllowUserOptOut = true`).

## Registration

```csharp
builder.AddGranitModule<GranitHostnamesNotificationsModule>();
```

Requires `Granit.Notifications.Abstractions` and an `INotificationPublisher` implementation
(e.g. `Granit.Notifications.Email.Smtp`) to be registered separately.
