# Granit.Events.Wolverine

Wolverine-backed event bus providers for the Granit framework.
Replaces the in-process defaults with `WolverineLocalEventBus` (local queue)
and `WolverineDistributedEventBus` (outbox-backed, at-least-once delivery).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Events.Wolverine
```

## Dependencies

- `Granit.Events`
- `Granit.Wolverine`

For **durable distributed delivery** (`WolverineDistributedEventBus` / outbox) you must
also add a transport provider: `Granit.Wolverine.Postgresql` (recommended, ISO 27001) or
`Granit.Wolverine.SqlServer`. Without one, only in-process local delivery
(`WolverineLocalEventBus`) is available.

## Integration

1. Add the bus to your root host module:

   ```csharp
   [DependsOn(typeof(GranitEventsWolverineModule))]
   public sealed class MyAppModule : GranitModule { }
   ```

2. Add a transport provider — this is **not** pulled transitively and is required for
   outbox durability:

   ```csharp
   [DependsOn(typeof(GranitWolverinePostgresqlModule))] // or GranitWolverineSqlServerModule
   ```

3. Configure the transport's section in `appsettings.json` (see the
   `Granit.Wolverine.Postgresql` configuration section):

   ```json
   {
     "Wolverine": {
       "Postgresql": { "TransportConnectionStringName": "DefaultConnection" }
     }
   }
   ```

Local events (`IDomainEvent`) route to a Wolverine local queue; distributed events
(`IIntegrationEvent`) flow through the outbox for durable at-least-once delivery.

## Documentation

See the [full documentation](https://granit-fx.dev).
