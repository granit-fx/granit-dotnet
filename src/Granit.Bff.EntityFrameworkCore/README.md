# Granit.Bff.EntityFrameworkCore

EF Core persistence layer for BFF session tokens. Alternative to the default
`IDistributedCache`-backed store for deployments without Redis.

## Features

- `BffDbContext` — isolated DbContext with `BffSession` entity
- `EfCoreBffTokenStore` — `IBffTokenStore` implementation backed by EF Core
- Expired session cleanup handler
- Queryable sessions (list by user, revoke by user)
