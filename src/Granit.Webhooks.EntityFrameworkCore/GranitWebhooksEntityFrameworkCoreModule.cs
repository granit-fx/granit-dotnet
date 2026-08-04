using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore;

/// <summary>
/// Granit module that enables EF Core persistence for <c>Granit.Webhooks</c>.
/// </summary>
/// <remarks>
/// Replaces the default InMemory/no-op stores with durable PostgreSQL implementations.
/// The application must configure the DbContext via
/// <c>AddGranitWebhooksEntityFrameworkCore(opts => opts.UseNpgsql(connectionString))</c>.
/// </remarks>
[DependsOn(
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitWebhooksModule))]
public sealed class GranitWebhooksEntityFrameworkCoreModule : GranitModule;
