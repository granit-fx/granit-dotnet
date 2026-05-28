using Granit.Modularity;
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
/// implementation lands in Phase 2B of Epic #2377.
/// </remarks>
[DependsOn(
    typeof(GranitWebhooksModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitWebhooksEntityFrameworkCoreModule : GranitModule;
