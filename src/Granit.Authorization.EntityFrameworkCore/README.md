# Granit.Authorization.EntityFrameworkCore

EF Core persistence for Granit.Authorization permission grants. Provides PermissionGrant entity, IPermissionGrantDbContext, and IPermissionGrantStore backed by EF Core.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Authorization.EntityFrameworkCore
```

## Dependencies

- `Granit.Authorization`
- `Granit.Persistence`

## Usage

Three steps fold the permission-grant tables into your host `DbContext`:

1. Implement `IPermissionGrantDbContext` on the host `DbContext` and expose the
   grants set. `RoleMetadata` has a default interface implementation, so only
   `PermissionGrants` is mandatory:

   ```csharp
   public sealed class MyAppDbContext : GranitDbContext, IPermissionGrantDbContext
   {
       public DbSet<PermissionGrant> PermissionGrants => Set<PermissionGrant>();
   }
   ```

2. Call `ConfigureAuthorizationModule()` in `OnModelCreating` (or
   `OnGranitModelCreating`) so the `permission_grants` table is mapped:

   ```csharp
   modelBuilder.ConfigureAuthorizationModule();
   ```

3. Register the store, swapping the no-op default:

   ```csharp
   services.AddGranitAuthorizationEntityFrameworkCore<MyAppDbContext>();
   ```

The generic constraint (`where TContext : DbContext, IPermissionGrantDbContext`)
enforces step 1 at compile time; omitting step 2 leaves the table unmapped.

## Documentation

See the [full documentation](https://granit-fx.dev).
