# Granit.UserSessions.EntityFrameworkCore

EF Core durable implementation of the `Granit.UserSessions` `ISessionRiskStore`. Persists
session anomaly risk verdicts so a `Medium`/`High` verdict survives application restarts and
pod evictions, and is shared across instances — keeping the risk shown on the BFF and identity
session surfaces stable.

Replaces the in-memory, single-node default from `Granit.UserSessions.Abstractions`.

Part of the [granit](https://granit-fx.dev) framework.

## How it works

Verdicts are stored in an isolated `SessionRiskDbContext`, one row per `(userId, sessionId)`,
upserted on each assessment. The risk level is stored as its PascalCase name for SQL-audit
readability; reason codes are stored as a JSON array.

## Registration

```csharp
// Program.cs
builder.AddGranitUserSessionsEntityFrameworkCore(options => options.UseNpgsql(connectionString));
```

The host owns the database migration (framework packages never ship migrations).

## Dependencies

- `Granit.UserSessions.Abstractions` (the `ISessionRiskStore` contract)
- `Granit.Persistence.EntityFrameworkCore`
- `Granit.Guids`

## License

Apache-2.0
