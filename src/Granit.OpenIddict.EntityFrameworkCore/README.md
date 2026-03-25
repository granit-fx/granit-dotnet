# Granit.OpenIddict.EntityFrameworkCore

EF Core persistence layer for the Granit OpenIddict module. Provides the isolated
`OpenIddictDbContext`, Identity entities, and tenant-isolated stores.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.OpenIddict.EntityFrameworkCore
```

## What's in this package

- **`OpenIddictDbContext`** — isolated DbContext extending `IdentityDbContext<GranitUser, GranitRole, Guid>`
- **Entities** — `GranitUser`, `GranitRole`, `GranitUserGroup`, `GranitUserGroupMember`
- **`GranitOpenIddictDbProperties`** — configurable table prefix (`openiddict_*`) and schema
- **`ConfigureOpenIddictModule()`** — model builder extension for table remapping and filters
- **ASP.NET Core Identity** — fully configured with `AddIdentity<GranitUser, GranitRole>()`
- **OpenIddict Core** — EF Core stores with `DisableEntityCaching()` (multi-tenant safe)

## Key constraints

- `GranitUser` does NOT implement `ISoftDeletable` — uses manual `IsDeleted` + named query filter
- `GranitUser` does NOT implement `IConcurrencyAware` — Identity manages its own `ConcurrencyStamp`
- `DisableEntityCaching()` is mandatory in multi-tenant setups (prevents cross-tenant cache pollution)
- All tables are prefixed with `openiddict_` by default (configurable via `GranitOpenIddictDbProperties`)

## Dependencies

- `Granit.Encryption`
- `Granit.MultiTenancy`
- `Granit.OpenIddict.Server`
- `Granit.Persistence`

## Documentation

See the [full documentation](https://granit-fx.dev).
