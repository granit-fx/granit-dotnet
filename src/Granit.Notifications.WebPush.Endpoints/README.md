# Granit.Notifications.WebPush.Endpoints

Minimal API REST endpoints for browser **Web Push** (W3C, VAPID) subscription management.

Opt-in companion to `Granit.Notifications.Endpoints`: it keeps the Web Push channel (and its
`Lib.Net.Http.WebPush` dependency) out of hosts that do not use Web Push. Reference this package
only when you expose browser push subscription endpoints.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.WebPush.Endpoints
```

## Dependencies

- `Granit.Notifications.Endpoints`
- `Granit.Notifications.WebPush`
- `Granit.Validation`

## Integration

Reach the module through your host's `[DependsOn]` graph:

```csharp
[DependsOn(typeof(GranitNotificationsWebPushEndpointsModule))]
public sealed class YourHostModule : GranitModule;
```

Register the Web Push channel (`AddGranitNotificationsPush()`), then map the endpoints during
routing setup:

```csharp
app.MapGranitWebPushSubscriptions();
```

This exposes, under the configured prefix (default `notifications`):

- `POST /notifications/push/subscriptions` — register a subscription (W3C `PushSubscriptionJSON`
  body). Returns `201 Created` for a new subscription, `200 OK` for an update.
- `DELETE /notifications/push/subscriptions` — unregister by endpoint (JSON body `{ "endpoint": … }`).

Both routes require the `Notifications.UserNotifications.Manage` permission.

## Documentation

See the [full documentation](https://granit-fx.dev).
