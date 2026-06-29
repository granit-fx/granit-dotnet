# Granit.BackgroundJobs.EntityFrameworkCore

EF Core persistence layer for Granit.BackgroundJobs. Provides BackgroundJobsDbContext and EfBackgroundJobStore.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.BackgroundJobs.EntityFrameworkCore
```

## Dependencies

- `Granit.BackgroundJobs`
- `Granit.Guids`
- `Granit.Persistence`

## Configuration

Two wiring steps make EF Core the durable store for background jobs:

1. Register the EF Core store **after** `AddGranitBackgroundJobs()` in the host.
   This replaces the default `InMemoryBackgroundJobStore` with
   `EfBackgroundJobStore`:

   ```csharp
   builder.AddGranitBackgroundJobs();
   builder.AddGranitBackgroundJobsEntityFrameworkCore(options =>
       options.UseNpgsql(connectionString));
   ```

2. Call `modelBuilder.ConfigureBackgroundJobsModule()` inside the host
   DbContext's `OnModelCreating` (or `OnGranitModelCreating`) so migrations
   include the `BackgroundJobDefinition` table. Optionally set
   `GranitBackgroundJobsDbProperties.DbSchema` / `DbTablePrefix` beforehand to
   customise naming.

The provider and connection string are supplied solely through the configure
delegate — there is no `BackgroundJobs:Durable` connection-string sub-section.

## Documentation

See the [full documentation](https://granit-fx.dev).
