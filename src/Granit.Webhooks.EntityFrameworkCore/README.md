# Granit.Webhooks.EntityFrameworkCore

EF Core persistence layer for Granit.Webhooks. Provides EfWebhookSubscriptionStore and EfWebhookDeliveryStore with ISO 27001 audit trail.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Webhooks.EntityFrameworkCore
```

## Dependencies

- `Granit.Persistence`
- `Granit.Webhooks`

## Integration

Webhooks is a **dual-scope** module: platform-managed subscriptions (`TenantId == null`)
and tenant-managed subscriptions (`TenantId == <tenant>`) coexist in the same physical
table. Tenant isolation is enforced by the row-level `MultiTenant` query filter; host
admin endpoints bypass the filter through `EfWebhookSubscriptionQueryableSource`.

To support this contract the tables live in `GranitDbDefaults.HostDbSchema` (default
`host`) — never per-tenant. When folding the entity configurations into your own
`DbContext`, call `ConfigureWebhooksModule()` on a **host-scoped** DbContext:

```csharp
public sealed class AppHostDbContext(
    DbContextOptions<AppHostDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null) : GranitDbContext(options, currentTenant, dataFilter)
{
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureWebhooksModule(); // host DbContext owns the tables
    }
}
```

```csharp
// Program.cs — host DbContext registered via AddGranitDbContext (non-isolated)
builder.Services.AddGranitDbContext<AppHostDbContext>(opts => opts.UseNpgsql(cs));
builder.AddGranitWebhooksEntityFrameworkCore(opts => opts.UseNpgsql(cs));
```

**Never** fold `ConfigureWebhooksModule()` into a tenant-isolated DbContext
(`AddGranitIsolatedDbContext<T>`). Under `SchemaPerTenant` or `DatabasePerTenant` this
creates the table in each tenant's schema (e.g. `acme.webhooks_subscriptions`) while
the internal `WebhooksDbContext` still qualifies queries against `host.webhooks_subscriptions`,
producing PostgreSQL `42P01 relation does not exist` at the first request.

`WebhooksDualScopeIntegrationValidator` fails fast at host startup when this
misconfiguration is detected.

### Why not per-tenant tables?

The row-level filter already gives tenants logical isolation: a subscription written by
`user@acme.local` carries `TenantId = <acme-guid>` and is invisible to `beta` tenant
queries. The single shared table also lets host admins observe events cross-tenant
(e.g. SIEM forwarding) without duplicating the schema.

If your deployment needs **physical** isolation (e.g. `DROP SCHEMA` tenant lessivage
under GDPR), this is a separate Epic — the framework currently does not ship a
fully-isolated mode for Webhooks.

## Documentation

See the [full documentation](https://granit-fx.dev).
