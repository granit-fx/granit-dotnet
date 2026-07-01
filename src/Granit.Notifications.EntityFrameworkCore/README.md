# Granit.Notifications.EntityFrameworkCore

EF Core persistence for Granit.Notifications. Provides PostgreSQL-backed stores for
`IUserNotificationStore`, `INotificationPreferenceStore`, `INotificationSubscriptionStore`,
and `INotificationDeliveryStore` (ISO 27001 audit trail). Includes `EntityTrackingInterceptor`
for automatic entity change tracking.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.EntityFrameworkCore
```

## Dependencies

- `Granit.Notifications`
- `Granit.Persistence`

## Documentation

See the [full documentation](https://granit-fx.dev).
