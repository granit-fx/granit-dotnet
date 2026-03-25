---
title: "Isolated DbContext Per Module: Why and How"
date: 2026-02-18
sidebar:
  order: 3
authors:
  - jfmeyers
tags:
  - architecture
  - persistence
description: "Shared DbContexts create hidden coupling between modules. Here is how Granit enforces database isolation per module — and why it makes microservice extraction mechanical."
excerpt: >
  Shared DbContexts create hidden coupling between modules. Here is how Granit
  enforces database isolation per module — and why it makes microservice extraction
  mechanical.
---

You have fifteen EF Core entity types in a single `AppDbContext`. Notifications reference users. Workflows reference blob storage metadata. Localization overrides sit next to webhook subscriptions. Everything compiles. Everything works. And then you try to extract the notification module into its own service.

You discover that removing `NotificationEntity` from the shared context breaks three unrelated migrations. A query filter on `ISoftDeletable` that worked fine in the monolith now throws because the interceptor was registered on the wrong context. The `OnModelCreating` method is 400 lines long, and half of it belongs to modules you are not extracting.

This is the **shared DbContext trap**. Granit avoids it entirely by enforcing **one isolated DbContext per module**.

## What "isolated" means in practice

Each Granit module that persists data owns a dedicated `DbContext` subclass. That context knows about exactly the entities the module owns — nothing more. It has its own migration history, its own schema configuration, and its own connection lifetime.

Here is the real `DbContext` from the Localization module:

```csharp title="LocalizationDbContext.cs"
internal sealed class LocalizationDbContext(
    DbContextOptions<LocalizationDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<LocalizationOverride> LocalizationOverrides { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new LocalizationOverrideConfiguration());
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
```

Notice what is **not** here: no `DbSet<WebhookSubscription>`, no `DbSet<BackgroundJob>`, no entity types from other modules. The localization context is a closed unit. It compiles, migrates, and runs independently of every other module in the system.

The context is `internal sealed`. No external code can reference it directly. Consumers interact through the module's public abstractions — never through the `DbContext`.

## The three pillars of the pattern

Every isolated DbContext in Granit follows a strict checklist. Break any rule and the architecture tests fail.

### 1. Constructor injection of cross-cutting services

The constructor accepts `ICurrentTenant?` and `IDataFilter?` as **optional parameters** (defaulting to `null`). This is deliberate: a module should work whether or not multi-tenancy is configured in the host application.

`ICurrentTenant` lives in `Granit.MultiTenancy`, not in `Granit.MultiTenancy`. Every module can consume it without taking a hard dependency on the multi-tenancy package. When multi-tenancy is not installed, a `NullTenantContext` with `IsAvailable = false` is injected — the query filter simply does not apply.

`IDataFilter` enables runtime filter bypass. An admin endpoint that needs to see soft-deleted records can call `dataFilter.Disable<ISoftDeletable>()` within a scope, and the filter is suppressed for that query only.

### 2. ApplyGranitConventions — centralized query filters

The call to `modelBuilder.ApplyGranitConventions(currentTenant, dataFilter)` at the end of `OnModelCreating` is where the framework applies **global query filters** for five marker interfaces:

- **`ISoftDeletable`** — filters out records where `IsDeleted == true`
- **`IActive`** — filters out records where `IsActive == false`
- **`IProcessingRestrictable`** — filters out GDPR-restricted records
- **`IMultiTenant`** — scopes queries to `currentTenant.Id`
- **`IPublishable`** — filters out unpublished records

Since EF Core 10, the implementation registers one **named** `HasQueryFilter` per
applicable interface per entity — each filter is independent and individually
bypassable per query via `IgnoreQueryFilters([GranitFilterNames.SoftDelete])`.
Before EF Core 10, `HasQueryFilter()` silently overwrote the previous filter,
requiring a combined `AndAlso` expression. Named filters are the idiomatic
EF Core 10 solution — `ApplyGranitConventions` uses them internally.

Each filter follows the pattern `bypass || realCondition`, where `bypass` reads a
property on a `FilterProxy` object captured as a constant expression. EF Core
evaluates this property on every query execution (it treats simple property access
on a `ConstantExpression` as a parameterized value), so toggling
`IDataFilter.Disable<T>()` at runtime takes effect immediately without rebuilding
the model.

**You must never write manual `HasQueryFilter` calls in your entity configurations.**
They conflict with the named filter keys registered by `ApplyGranitConventions`,
breaking tenant isolation or soft delete. This rule is documented, tested, and
enforced by code review.

### 3. Registration through AddGranitDbContext

Each module registers its DbContext through a single extension method:

```csharp title="PersistenceDbContextServiceCollectionExtensions.cs"
public static IServiceCollection AddGranitDbContext<TContext>(
    this IServiceCollection services,
    Action<DbContextOptionsBuilder> configure)
    where TContext : DbContext
{
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configure);

    services.AddDbContextFactory<TContext>((sp, options) =>
    {
        configure(options);
        options.UseGranitInterceptors(sp);
    }, ServiceLifetime.Scoped);

    return services;
}
```

This method does three things right:

- It uses `AddDbContextFactory<TContext>` with the **`(IServiceProvider, DbContextOptionsBuilder)` overload**, so interceptors can be resolved from DI.
- It calls `UseGranitInterceptors(sp)`, which wires the four standard interceptors in the correct order.
- It sets `ServiceLifetime.Scoped`, so the factory produces one context per scope (per HTTP request, per message handler).

The module's own service registration becomes a one-liner:

```csharp title="LocalizationEntityFrameworkCoreHostApplicationBuilderExtensions.cs"
services.AddGranitDbContext<LocalizationDbContext>(
    options => options.UseNpgsql(connectionString));
```

## Interceptor ordering matters

`UseGranitInterceptors` adds four interceptors in a fixed sequence:

1. **`AuditedEntityInterceptor`** — sets `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`, `TenantId`, and `Id` (sequential GUID) on new or modified entities. ISO 27001 compliance.
2. **`VersioningInterceptor`** — assigns `VersionId` and increments `Version` on `IVersioned` entities.
3. **`DomainEventDispatcherInterceptor`** — collects domain events from entities after `SaveChanges` and dispatches them through Wolverine.
4. **`SoftDeleteInterceptor`** — converts physical deletes to soft deletes for `ISoftDeletable` entities by flipping `IsDeleted = true` and changing the entry state from `Deleted` to `Modified`.

**SoftDelete must be last.** It changes the entity state from `Deleted` to `Modified`. If it ran before the audit interceptor, the audit interceptor would see a "modified" record instead of a "deleted" one and write the wrong audit fields. The ordering is not configurable — it is baked into `UseGranitInterceptors` to prevent accidents.

Each interceptor is resolved via `GetService<T>()` and silently skipped if not registered. This means a module can call `UseGranitInterceptors` even if the host has not configured all of `Granit.Persistence` — the interceptors that exist are wired, and the rest are no-ops.

## How stores consume the DbContext

Module stores never inject the `DbContext` directly. They inject `IDbContextFactory<TContext>` and create short-lived context instances:

```csharp title="EfWebhookSubscriptionStore.cs"
internal sealed class EfWebhookSubscriptionStore(
    IDbContextFactory<WebhooksDbContext> contextFactory)
{
    public async Task<WebhookSubscription?> GetOrNullAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }
}
```

This pattern gives you explicit control over the context lifetime. Each method creates a fresh context, executes its query, and disposes it. No ambient context leaking across operations. No stale change trackers accumulating entities from unrelated operations.

The store itself is `internal`. External consumers use the module's public interface (`IWebhookSubscriptionReader`, `IWebhookSubscriptionWriter`). The EF Core dependency is an implementation detail that does not leak into the module's API surface.

## Why this makes microservice extraction mechanical

When every module owns its data access in isolation, extracting a module into a standalone service is a checklist, not a redesign:

1. **Copy the module's `*.EntityFrameworkCore` project** into the new service.
2. **Point its `AddGranitDbContext` call** at the new service's database connection string.
3. **Run `dotnet ef migrations add Initial`** — the migration contains only the module's tables because the DbContext only knows about the module's entities.
4. **Replace cross-module calls** (the ones that went through in-process interfaces) with HTTP or messaging. The module boundaries already tell you exactly which calls cross modules.

Step 4 is the only one that requires thought. Steps 1 through 3 are mechanical because the data layer has no cross-module dependencies to untangle.

Contrast this with a shared `AppDbContext` approach. Extracting a module means surgically removing entity types from a monolithic context, rewriting migration history, hunting down navigation properties that cross module boundaries, and hoping the remaining query filters still compose correctly. It is the kind of work that gets estimated in weeks and delivered in months.

## The cost and why it is worth paying

The isolated DbContext pattern is not free. You pay for it in three ways:

- **More DbContext classes.** Granit has 12+ isolated contexts today. Each one is small (typically under 30 lines), but they exist.
- **More migration histories.** Each context has its own `__EFMigrationsHistory` table. Schema management tooling must be aware of this.
- **No cross-context joins.** You cannot write a LINQ query that joins `LocalizationOverrides` with `WebhookSubscriptions`. If you need data from another module, you go through its public interface.

The third point is the most important, and it is a feature, not a bug. Cross-module joins are the number one source of hidden coupling in data access layers. Forbidding them at the type system level (the entities simply do not exist in the other context) is stronger than any code review guideline.

The 12 modules that use this pattern in Granit today — Authorization, BackgroundJobs, BlobStorage, DataExchange, Features, Identity, Localization, Notifications, Settings, Templating, Timeline, Webhooks, and Workflow — all follow the same structure. Once you have seen one, you have seen them all. That consistency is the real payoff: onboarding a new developer to any module's persistence layer takes minutes, not days.

## The mandatory checklist

If you are creating a new `*.EntityFrameworkCore` package in Granit, every item on this list is required:

1. Add a `<ProjectReference>` to `Granit.Persistence` in the `.csproj`.
2. Accept `ICurrentTenant?` and `IDataFilter?` as optional constructor parameters (default `null`).
3. Call `modelBuilder.ApplyGranitConventions(currentTenant, dataFilter)` at the end of `OnModelCreating`.
4. Register the context via `AddGranitDbContext<TContext>` — never call `AddDbContextFactory` manually.
5. Add `[DependsOn(typeof(GranitPersistenceModule))]` on the module class.
6. Never write manual `HasQueryFilter` calls — `ApplyGranitConventions` handles all standard filters.
7. Use `Guid? TenantId` for `IMultiTenant` entities (never `string`).

Skip any of these and you will get silent filter bugs, missing audit trails, or broken tenant isolation. The checklist exists because each item was learned the hard way.

## Further reading

- [Persistence module reference](/dotnet/data/persistence/) — full API documentation for `AddGranitDbContext`, `UseGranitInterceptors`, and `ApplyGranitConventions`
- [Layered architecture pattern](/dotnet/architecture/patterns/layered-architecture/) — how modules are structured and why the EF Core layer is always an internal implementation detail
