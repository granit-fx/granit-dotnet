using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore;

/// <summary>
/// Granit module that enables EF Core persistence for <c>Granit.Webhooks</c>.
/// </summary>
/// <remarks>
/// <para>
/// Replaces the default InMemory/no-op stores with durable PostgreSQL implementations.
/// The application must configure the DbContext via
/// <c>AddGranitWebhooksEntityFrameworkCore(opts => opts.Configure = b => b.UseNpgsql(connectionString))</c>
/// (Shared mode, default). Per ADR-063 the <c>StorageMode</c> option also accepts
/// <c>DualScopeStorageMode.Segregated</c> for physical host/tenant separation —
/// implementation completed across Phases 2B (#2377) and 2C (cross-tenant host-admin
/// aggregation via <c>ITenantEnumerator</c>).
/// </para>
/// <para>
/// <c>Granit.MultiTenancy</c> is intentionally NOT a hard dependency: cross-tenant
/// aggregation routes through the framework-primitive <c>ITenantEnumerator</c>
/// (default <c>NullTenantEnumerator</c>) so single-tenant deployments can consume the
/// module without pulling the multi-tenant infrastructure. The full
/// <c>Granit.MultiTenancy</c> registration replaces the default enumerator with an
/// <c>ITenantReader</c>-backed adapter; without it, the Segregated host-admin browse
/// gracefully degrades to host-only results.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitWebhooksModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitWebhooksEntityFrameworkCoreModule : GranitModule;
