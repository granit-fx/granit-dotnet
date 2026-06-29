# Granit.Notifications.Endpoints

Minimal API REST endpoints for Granit.Notifications. Provides inbox (paginated notifications,
unread count, mark as read), per-entity activity feed, user preferences,
subscription management, and entity follower management.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.Endpoints
```

## Dependencies

- `Granit.Http.ApiDocumentation`
- `Granit.Guids`
- `Granit.Notifications`
- `Granit.Notifications.MobilePush`
- `Granit.Validation`

## Integration

Reach the module through your host's `[DependsOn]` graph:

```csharp
[DependsOn(typeof(GranitNotificationsEndpointsModule))]
public sealed class YourHostModule : GranitModule;
```

Then map the endpoints during routing setup (on `app` or a route group):

```csharp
app.MapGranitNotifications();
```

Without this call, the notification REST endpoints (inbox, preferences,
subscriptions, activity feed, entity followers) are never registered and return
404.

## Documentation

See the [full documentation](https://granit-fx.dev).
