# Granit.Notifications.MobilePush.Endpoints

Minimal API REST endpoints for **mobile push** device token management (FCM / APNs).

Opt-in companion to `Granit.Notifications.Endpoints`: it keeps the mobile push channel out of hosts
that do not use it. Reference this package only when you expose mobile push token endpoints.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.MobilePush.Endpoints
```

## Dependencies

- `Granit.Notifications.Endpoints`
- `Granit.Notifications.MobilePush`
- `Granit.Validation`

## Integration

Reach the module through your host's `[DependsOn]` graph:

```csharp
[DependsOn(typeof(GranitNotificationsMobilePushEndpointsModule))]
public sealed class YourHostModule : GranitModule;
```

Register the mobile push channel (`AddGranitNotificationsMobilePush()`), then map the endpoints
during routing setup:

```csharp
app.MapGranitMobilePushTokens();
```

This exposes, under the configured prefix (default `notifications/mobile-push`):

- `POST /tokens` — register a device token (upsert). Returns `201 Created` for a new token, `200 OK`
  for an update.
- `DELETE /tokens/{deviceToken}` — remove a device token.
- `GET /tokens` — list the current user's registered tokens (device token returned masked).

Mutations require the `Notifications.UserNotifications.Manage` permission; reads require
`Notifications.UserNotifications.Read`.

## Documentation

See the [full documentation](https://granit-fx.dev).
