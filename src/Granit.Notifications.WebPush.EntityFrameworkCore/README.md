# Granit.Notifications.WebPush.EntityFrameworkCore

EF Core persistence for the Granit **W3C Web Push (VAPID)** channel (browser push subscriptions).

Opt-in companion to `Granit.Notifications.EntityFrameworkCore`: it persists browser push
subscriptions so they survive restarts, keeping the store and its table out of hosts that do not
use the channel. The ECDH key material is encrypted at rest; the push endpoint is the natural
upsert / remove key.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.WebPush.EntityFrameworkCore
```

## Dependencies

- `Granit.Encryption.EntityFrameworkCore`
- `Granit.Notifications.EntityFrameworkCore`
- `Granit.Notifications.WebPush`
- `Granit.Persistence.EntityFrameworkCore`

## Integration

Reach the module through your host's `[DependsOn]` graph:

```csharp
[DependsOn(typeof(GranitNotificationsWebPushEntityFrameworkCoreModule))]
public sealed class YourHostModule : GranitModule;
```

Register the Web Push channel (`AddGranitNotificationsWebPush()`), then replace the in-memory
subscription store with the durable EF Core store:

```csharp
builder.AddGranitNotificationsWebPushEntityFrameworkCore(options =>
    options.UseNpgsql(connectionString));
```

Include the entity in the host-owned migration `DbContext`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder) =>
    modelBuilder.ConfigureWebPushModule();
```

## Documentation

See the [full documentation](https://granit-fx.dev).
