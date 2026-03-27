# Granit.Persistence.Hosting

EF Core migration runner with `--migrate` CLI support for Granit applications.

## Features

- **`--migrate` CLI mode** — apply pending migrations, seed data, exit cleanly
- **Automatic discovery** — finds `IMigratableModule<TContext>` in the module dependency graph
- **Internal DbContext support** — discovers `IInternalDbContextEnsurer` for isolated contexts (e.g., OpenIddict)
- **Distributed locking** — PostgreSQL advisory lock prevents concurrent migrations
- **Multi-tenant** — supports schema-per-tenant and DB-per-tenant via `ITenantEnumerator`
- **Retry with backoff** — handles transient database connection failures
- **Expand & Contract compatible** — handles DDL, complements runtime data backfill

## Quick start

```csharp
await builder.AddGranitAsync<AppHostModule>();
builder.AddGranitMigrateSupport();

var app = builder.Build();
await app.UseGranitAsync();

if (app.HasGranitMigrateFlag())
{
    await app.RunGranitMigrationsAsync();
    return;
}

await app.RunAsync();
```

## Usage

```bash
dotnet run --migrate
```
