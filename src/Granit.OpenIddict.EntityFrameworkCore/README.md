# Granit.OpenIddict.EntityFrameworkCore

EF Core persistence layer for the Granit OpenIddict module family.

## What's in this package

- **`OpenIddictDbContext`** — isolated DbContext extending `IdentityDbContext<GranitUser, GranitRole, Guid>`
- **Entities** — `GranitUser`, `GranitRole`, `GranitUserGroup`, `GranitUserGroupMember`
- **`GranitOpenIddictDbProperties`** — configurable table prefix (`oidc_*`) and schema
- **`ConfigureOpenIddictModule()`** — model builder extension for table remapping and filters
- **ASP.NET Core Identity** — fully configured with `AddIdentity<GranitUser, GranitRole>()`
- **OpenIddict Core** — EF Core stores with `DisableEntityCaching()` (multi-tenant safe)

## Usage

```csharp
builder.AddGranitOpenIddictEntityFrameworkCore(options =>
    options.UseNpgsql(connectionString));
```

## Migrations

Migrations are the host application's responsibility:

```bash
dotnet ef migrations add Initial --context OpenIddictDbContext
```

## Key constraints

- `GranitUser` does NOT implement `ISoftDeletable` — uses manual `IsDeleted` + named query filter
- `GranitUser` does NOT implement `IConcurrencyAware` — Identity manages its own `ConcurrencyStamp`
- `DisableEntityCaching()` is mandatory in multi-tenant setups (prevents cross-tenant cache pollution)
- All tables are prefixed with `oidc_` by default (configurable via `GranitOpenIddictDbProperties`)
