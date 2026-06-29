# Granit.Settings.EntityFrameworkCore

EF Core persistence for Granit.Settings via a dedicated, sealed internal `SettingsDbContext`.
The consuming application registers EF Core backing with `AddGranitSettingsEntityFrameworkCore`
and folds the Settings model into its own migration-owning DbContext using
`ConfigureSettingsModule()`. ISO 27001 audit via `AuditedEntityInterceptor`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Settings.EntityFrameworkCore
```

## Integration

1. In your host builder's `ConfigureServices`, register the EF Core store
   (this replaces the default in-memory store with the EF Core one):

   ```csharp
   context.Builder.AddGranitSettingsEntityFrameworkCore(options =>
       options.Configure = db => db.UseNpgsql(connectionString));
   ```

2. In your migration-owning DbContext's `OnModelCreating` (or
   `OnGranitModelCreating`), emit the `settings_setting_records` table:

   ```csharp
   modelBuilder.ConfigureSettingsModule();
   ```

Without `AddGranitSettingsEntityFrameworkCore`, the base module keeps its
default in-memory store (`InMemorySettingStore`) — settings are not persisted.

## Dependencies

- `Granit.Persistence`
- `Granit.Settings`

## Documentation

See the [full documentation](https://granit-fx.dev).
