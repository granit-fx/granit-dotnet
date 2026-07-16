# Granit.Identity.Local.EntityFrameworkCore

EF Core persistence for `Granit.Identity.Local`. Provides the isolated `IdentityLocalDbContext`
(users, roles, claims, logins, tokens, passkeys and groups), ASP.NET Core Identity stores backed by
it, the group store, the role/user-group query sources, and the DbContext accessor used for
cross-module transaction sharing.

Built on `GranitDbContext` (parameterised multi-tenant filter, null-is-global for global users and
groups). The model is declared self-contained by `ConfigureGranitIdentityLocal`, so a host migration
context can own the whole schema by composing it with `ConfigureGranitOpenIddict`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Identity.Local.EntityFrameworkCore
```

## Dependencies

- `Granit.Identity.Local`
- `Granit.MultiTenancy`
- `Granit.Persistence.EntityFrameworkCore`
