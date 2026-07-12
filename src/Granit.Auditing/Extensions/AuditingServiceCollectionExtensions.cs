using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.Exports;
using Granit.Auditing.Options;
using Granit.Auditing.Queries;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Auditing.Extensions;

/// <summary>
/// Extensions for configuring Granit.Auditing services in the DI container.
/// </summary>
public static class AuditingServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Auditing module services: options binding, activity source registration,
    /// metrics, and the declarative query/export definitions.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configure">Optional delegate to customize <see cref="AuditingOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitAuditing(
        this IServiceCollection services,
        Action<AuditingOptions>? configure = null)
    {
        services
            .AddOptions<AuditingOptions>()
            .BindConfiguration(AuditingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<AuditingOptions>, AuditingOptionsValidator>());

        if (configure is not null)
        {
            services.Configure(configure);
        }

        GranitActivitySourceRegistry.Register(AuditingActivitySource.Name);

        // Metrics.
        services.TryAddSingleton<AuditingMetrics>();

        // Persistence pipeline lives in Granit.Auditing.EntityFrameworkCore: audit rows are
        // staged into the audited DbContext's own transaction (embedded mode) or persisted
        // synchronously post-commit through the isolated AuditingDbContext (standalone mode).
        // Retention cleanup runs as a distributed recurring job in
        // Granit.Auditing.BackgroundJobs (AuditRetentionCleanupJob) — not a per-pod
        // hosted service — so a multi-replica deployment purges exactly once per schedule.

        // Query + Export definitions (ADR-020: owned by the base module).
        services.AddQueryDefinition<AuditEntry, AuditEntryQueryDefinition>();
        services.AddQueryDefinition<AuditEntityChange, AuditEntityChangeQueryDefinition>();
        services.AddExportDefinition<AuditEntry, AuditEntryExportDefinition>();
        services.AddExportDefinition<AuditEntityChange, AuditEntityChangeExportDefinition>();

        return services;
    }
}
