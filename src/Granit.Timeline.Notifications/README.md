# Granit.Timeline.Notifications

Notification adapter for Granit.Timeline. Bridges `ITimelineFollowerService` to
`INotificationSubscriptionStore` and `ITimelineNotifier` to `INotificationPublisher`
for durable follower management and multi-channel notification fan-out.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Timeline.Notifications
```

## Dependencies

- `Granit.Notifications`
- `Granit.Timeline`

## Integration

Reference the module on your host module so its services replace the default
no-op `ITimelineNotifier` and its email templates ship:

```csharp
[DependsOn(typeof(GranitTimelineNotificationsModule))]
public class AppModule : GranitModule { }
```

The transitive module dependencies (`GranitTimelineModule`,
`GranitNotificationsAbstractionsModule`, `GranitTemplatingModule`) are pulled in
automatically. Prerequisite: `Granit.Notifications` must be configured in your
infrastructure for the notification fan-out to actually deliver.

## Documentation

See the [full documentation](https://granit-fx.dev).
