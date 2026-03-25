# Granit.Auditing.EntityFrameworkCore

EF Core persistence for the Granit audit trail module.
Provides an isolated `AuditingDbContext` with hierarchical entity model,
change tracking interceptor, async/strict persistence modes,
`IAuditingReader` implementation, and category-based retention cleanup.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Auditing.EntityFrameworkCore
```

## Dependencies

- `Granit.Auditing`
- `Granit.Persistence`

## Documentation

See the [full documentation](https://granit-fx.dev).
