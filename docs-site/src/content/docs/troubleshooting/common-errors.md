---
title: "Common Errors \u2014 Diagnostic & Resolution Guide"
description: Diagnose and fix common Granit errors — missing DependsOn, DbContext misconfiguration, FluentValidation registration, multi-tenancy setup, and OpenTelemetry tracing issues.
sidebar:
  label: Common Errors
  order: 1
---

## Module loading failures

### Missing `[DependsOn]` attribute

**Symptom**: `InvalidOperationException` at startup -- a service cannot be
resolved because the module that registers it was never loaded.

```text
System.InvalidOperationException: Unable to resolve service for type
'Granit.Caching.IDistributedCacheManager' while attempting to activate
'MyApp.Services.ProductService'.
```

**Cause**: Your module uses a Granit service but does not declare the dependency.

**Fix**: Add the `[DependsOn]` attribute to your module class.

```csharp
// Before -- missing dependency
public sealed class MyAppModule : GranitModule { }

// After
[DependsOn(typeof(GranitCachingModule))]
public sealed class MyAppModule : GranitModule { }
```

### Circular dependencies

**Symptom**: `InvalidOperationException` at startup listing the modules involved
in the cycle.

```text
System.InvalidOperationException: Circular dependency detected:
ModuleA -> ModuleB -> ModuleC -> ModuleA
```

**Fix**: Break the cycle by extracting shared abstractions into a separate
package. The dependent modules reference the abstractions package instead of each
other.

## DbContext configuration issues

### Missing `ApplyGranitConventions`

**Symptom**: Soft-deleted entities still appear in queries. Multi-tenant data
leaks across tenants. `IActive` and `IPublishable` filters do not apply.

**Cause**: The isolated `DbContext` does not call `ApplyGranitConventions` in
`OnModelCreating`.

**Fix**: Call `modelBuilder.ApplyGranitConventions(currentTenant, dataFilter)` at
the end of `OnModelCreating`.

```csharp
public class MyDbContext(
    DbContextOptions<MyDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MyDbContext).Assembly);

        // This line is mandatory
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
```

:::caution
Never add manual `HasQueryFilter` calls for `ISoftDeletable`, `IMultiTenant`,
`IActive`, or `IPublishable`. `ApplyGranitConventions` handles all standard
filters centrally. Manual filters cause duplicates or conflicts.
:::

### Missing interceptors

**Symptom**: `CreatedAt`, `CreatedBy`, `LastModifiedAt`, `LastModifiedBy` fields
are never populated. Soft-deleted entities are hard-deleted instead of flagged.

**Cause**: The `AddDbContextFactory` call does not resolve
`AuditedEntityInterceptor` and `SoftDeleteInterceptor`.

**Fix**: Use the `(sp, options)` overload to resolve interceptors from the
service provider.

```csharp
services.AddDbContextFactory<MyDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    options.AddInterceptors(
        sp.GetRequiredService<AuditedEntityInterceptor>(),
        sp.GetRequiredService<SoftDeleteInterceptor>());
}, ServiceLifetime.Scoped);
```

## Validator not executing

### Missing registration

**Symptom**: Invalid input passes through endpoints without triggering
`FluentValidation` rules. No validation errors are returned.

**Cause**: Validators are not registered in the DI container.
`FluentValidationEndpointFilter<T>` silently skips validation when no validator
is found for the request type.

**Fix**: The registration method depends on whether the module uses Wolverine.

**With Wolverine** (module has `[assembly: WolverineHandlerModule]`): Validators
are discovered automatically by `AddGranitWolverine()`. No manual registration
needed.

**Without Wolverine**: Call `AddGranitValidatorsFromAssemblyContaining<T>()`
manually in your module's `ConfigureServices`.

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services
        .AddGranitValidatorsFromAssemblyContaining<CreateProductRequestValidator>();
}
```

### Validator in the wrong assembly

**Symptom**: `AddGranitValidatorsFromAssemblyContaining<T>()` was called, but
some validators are still not running.

**Cause**: The type `T` used as the assembly marker is in a different assembly
from the missing validators.

**Fix**: Verify that the validator class lives in the same assembly as the marker
type. If validators are spread across multiple assemblies, call the registration
method once per assembly.

## Multi-tenancy errors

### `TenantId` is null

**Symptom**: `DbUpdateException` when inserting entities that implement
`IMultiTenant` -- the `TenantId` column has a NOT NULL constraint but the value
is null.

**Cause**: The `ICurrentTenant` context was not set before the operation. This
typically happens in background jobs or message handlers where the tenant context
is not propagated automatically.

**Fix**: Wrap the operation in a tenant scope.

```csharp
using (currentTenant.Change(tenantId))
{
    await dbContext.Products.AddAsync(product, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
}
```

### `IsAvailable` not checked

**Symptom**: `InvalidOperationException` when accessing `ICurrentTenant.Id` --
the tenant context is not available.

**Cause**: Code accesses `Id` directly without checking `IsAvailable` first.
When multi-tenancy is not installed, a `NullTenantContext` (`IsAvailable =
false`) is registered by default.

**Fix**: Always check `IsAvailable` before accessing `Id`.

```csharp
if (currentTenant.IsAvailable)
{
    var tenantId = currentTenant.Id;
    // tenant-specific logic
}
```

## Authentication issues

### JWT token rejected

**Symptom**: All authenticated requests return `401 Unauthorized` even with a
valid token.

**Common causes**:

1. **Wrong authority URL**: The `Authority` in `JwtBearerOptions` does not match
   the `iss` claim in the token.
2. **Clock skew**: The server clock is ahead of the token issuer. Default
   `ClockSkew` is 5 minutes.
3. **Audience mismatch**: The `ValidAudience` does not match the `aud` claim.

**Diagnosis**: Decode the token at [jwt.io](https://jwt.io) and compare the
`iss`, `aud`, and `exp` claims against your configuration.

### Keycloak claims not mapped

**Symptom**: `ICurrentUserService` returns null or empty values for user
properties (name, email, roles) even though the token contains them.

**Cause**: Keycloak uses non-standard claim names by default (e.g.,
`preferred_username` instead of `name`, `realm_access.roles` instead of `role`).

**Fix**: Ensure `GranitAuthenticationKeycloakModule` is included in your
dependency chain. It registers the `KeycloakClaimsTransformation` that maps
Keycloak-specific claims to standard ClaimTypes.

```csharp
[DependsOn(typeof(GranitAuthenticationKeycloakModule))]
public sealed class MyAppModule : GranitModule { }
```

## Caching issues

### Redis connection refused

**Symptom**: `RedisConnectionException` at startup or on the first cache
operation.

```text
StackExchange.Redis.RedisConnectionException: It was not possible to connect
to the redis server(s). UnableToConnect on localhost:6379/Interactive
```

**Common causes**:

1. Redis is not running or not reachable at the configured address
2. TLS is required but not configured
3. Password authentication is required but not provided

**Diagnosis**: Test connectivity with `redis-cli ping` from the application host.
Check the connection string in your configuration.

### HybridCache not invalidating

**Symptom**: Stale data is served from cache after updates.

**Common causes**:

1. **Missing cache key invalidation**: After a write operation, the
   corresponding cache entry was not removed or updated.
2. **L1/L2 desynchronization**: The in-memory (L1) cache on one instance is not
   aware of invalidation performed by another instance. HybridCache handles this
   via Redis pub/sub, but only if the Redis backplane is correctly configured.

**Fix**: Verify that `AddGranitHybridCache()` is called with a valid Redis
connection and that the Redis instance supports pub/sub (not all managed Redis
services enable it by default).
