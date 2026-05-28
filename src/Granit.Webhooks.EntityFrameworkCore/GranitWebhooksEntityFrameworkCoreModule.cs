using Granit.Modularity;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore;

/// <summary>
/// Granit module that enables EF Core persistence for <c>Granit.Webhooks</c>.
/// </summary>
/// <remarks>
/// Replaces the default InMemory/no-op stores with durable PostgreSQL implementations.
/// The application must configure the DbContext via
/// <c>AddGranitWebhooksEntityFrameworkCore(opts => opts.Configure = b => b.UseNpgsql(connectionString))</c>
/// (Shared mode, default). Per ADR-063 the <c>StorageMode</c> option also accepts
/// <c>DualScopeStorageMode.Segregated</c> for physical host/tenant separation —
/// implementation completed across Phases 2B (#2377) and 2C (cross-tenant host-admin
/// aggregation via <c>ITenantReader</c>).
/// </remarks>
[DependsOn(
    typeof(GranitWebhooksModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitMultiTenancyModule))]
public sealed class GranitWebhooksEntityFrameworkCoreModule : GranitModule;
