# Granit.Testing.EntityFrameworkCore

EF Core test factories for Granit with automatic interceptor wiring. Prefer `SqliteDbContextFactory` for most tests; use `InMemoryDbContextFactory` only when SQL semantics are irrelevant.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Testing.EntityFrameworkCore
```

## Dependencies

- `Granit.Testing`
- `Granit.Persistence`
- `Microsoft.EntityFrameworkCore.InMemory`
- `Microsoft.EntityFrameworkCore.Sqlite`

## Documentation

See the [full documentation](https://granit-fx.dev).
