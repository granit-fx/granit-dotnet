# Granit.AuditLog.EntityFrameworkCore

EF Core persistence for the Granit audit trail module.
Provides an isolated `AuditLogDbContext` with hierarchical entity model,
change tracking interceptor, async/strict persistence modes,
`IAuditLogReader` implementation, and category-based retention cleanup.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AuditLog.EntityFrameworkCore
```

## Dependencies

- `Granit.AuditLog`
- `Granit.Persistence`

## Documentation

See the [full documentation](https://granit-fx.dev).
