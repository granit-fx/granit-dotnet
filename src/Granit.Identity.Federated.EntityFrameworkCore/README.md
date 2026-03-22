# Granit.Identity.Federated.EntityFrameworkCore

EF Core persistence for Granit.Identity user cache. Provides `UserCacheEntry` entity, `IUserCacheDbContext`,
`CachedUserLookupService` with cache-aside strategy (including incremental stale refresh), login-time sync
middleware, and Wolverine event handlers for real-time identity provider sync.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Identity.Federated.EntityFrameworkCore
```

## Dependencies

- `Granit.Identity.Federated`
- `Granit.Persistence`
- `Granit.Security`

## Documentation

See the [full documentation](https://granit-fx.dev).
