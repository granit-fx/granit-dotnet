using Granit.Core.Modularity;
using Granit.Persistence;
using Granit.Webhooks.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore;

/// <summary>
/// Granit module that enables EF Core persistence for <c>Granit.Webhooks</c>.
/// </summary>
/// <remarks>
/// Replaces the default InMemory/no-op stores with durable PostgreSQL implementations.
/// The application must configure the DbContext via
/// <c>AddGranitWebhooksEntityFrameworkCore(opts => opts.UseNpgsql(connectionString))</c>
/// instead of using this module directly when custom DbContext options are needed.
/// </remarks>
[DependsOn(typeof(GranitPersistenceModule))]
[DependsOn(typeof(GranitWebhooksModule))]
public sealed class GranitWebhooksEntityFrameworkCoreModule : GranitModule
{
    private readonly Action<DbContextOptionsBuilder> _configure;

    /// <summary>
    /// Initializes the module with the EF Core DbContext configuration delegate.
    /// </summary>
    public GranitWebhooksEntityFrameworkCoreModule(Action<DbContextOptionsBuilder> configure) =>
        _configure = configure;

    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitWebhooksEntityFrameworkCore(_configure);
}
