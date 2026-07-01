# Granit.Notifications.MobilePush.EntityFrameworkCore

EF Core persistence for the Granit **mobile push** channel (FCM / APNs device tokens).

Opt-in companion to `Granit.Notifications.EntityFrameworkCore`: it keeps the mobile push token
store and its table out of hosts that do not use the channel. The device token is encrypted at
rest and upsert / remove route through a lookup hash.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.MobilePush.EntityFrameworkCore
```

## Dependencies

- `Granit.Encryption.EntityFrameworkCore`
- `Granit.Notifications.EntityFrameworkCore`
- `Granit.Notifications.MobilePush`
- `Granit.Persistence.EntityFrameworkCore`

## Integration

Reach the module through your host's `[DependsOn]` graph:

```csharp
[DependsOn(typeof(GranitNotificationsMobilePushEntityFrameworkCoreModule))]
public sealed class YourHostModule : GranitModule;
```

Register the mobile push channel (`AddGranitNotificationsMobilePush()`), then replace the in-memory
token store with the durable EF Core store:

```csharp
builder.AddGranitNotificationsMobilePushEntityFrameworkCore(options =>
    options.UseNpgsql(connectionString));
```

Include the entity in the host-owned migration `DbContext`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder) =>
    modelBuilder.ConfigureMobilePushModule();
```

## Documentation

See the [full documentation](https://granit-fx.dev).
