# Granit.BackgroundJobs

Recurring background jobs for Granit, declared via [RecurringJob]. In-process channel dispatch by default, or durable, cluster-safe Wolverine Outbox scheduling (opt-in via the Granit.BackgroundJobs.Wolverine package). Database-agnostic EF Core store (Granit.BackgroundJobs.EntityFrameworkCore) for durable persistence; the default in-memory store loses job state on restart and is intended for development. IBackgroundJobReader/IBackgroundJobWriter for administration (CQRS).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.BackgroundJobs
```

## Dependencies

- `Granit`
- `Granit.Guids`
- `Granit.Users`
- `Granit.Timing`

## Configuration

The module is bound from the `BackgroundJobs` section of `appsettings.json`. By
default it uses an **in-memory** store (`Mode: InMemory`) — no database, state
lost on restart, `ConnectionString` ignored. Ideal for development and tests.

For durable, cluster-safe persistence two steps are required:

1. Set the configuration section:

   ```json
   {
     "BackgroundJobs": {
       "Mode": "Durable",
       "ConnectionString": "<SQL Server or PostgreSQL connection string>"
     }
   }
   ```

2. Install `Granit.BackgroundJobs.EntityFrameworkCore` and call
   `builder.AddGranitBackgroundJobsEntityFrameworkCore(...)` after
   `AddGranitBackgroundJobs()`. This swaps the in-memory store for the EF Core
   store (the module creates and manages the `background_jobs_background_jobs`
   table).

Startup validation (`ValidateOnStart`) fails if `Mode` is `Durable` with an
empty `ConnectionString`.

## Documentation

See the [full documentation](https://granit-fx.dev).
