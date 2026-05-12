using Granit.Analytics.Extensions;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Documents.Diagnostics;
using Granit.Documents.Domain;
using Granit.Documents.Exports;
using Granit.Documents.Metrics;
using Granit.Documents.Options;
using Granit.Documents.Queries;
using Granit.QueryEngine.Extensions;
using Granit.Taxonomy.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for registering Granit.Documents module services.
/// </summary>
public static class DocumentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Granit.Documents module with the provided configuration.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configure">
    /// Optional callback to override the defaults of <see cref="GranitDocumentsOptions"/>.
    /// When omitted, options are bound from the <c>"Documents"</c> configuration section
    /// (when present) and validated on application start.
    /// </param>
    /// <remarks>
    /// Phase-1 scaffolding registration: option binding plus diagnostics
    /// (<see cref="DocumentsMetrics"/> meter and <c>Granit.Documents</c>
    /// <see cref="System.Diagnostics.ActivitySource"/>). Domain services, endpoints,
    /// and persistence are wired by subsequent stories of the Granit.Documents Epic.
    /// </remarks>
    public static IServiceCollection AddGranitDocuments(
        this IServiceCollection services,
        Action<GranitDocumentsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        GranitActivitySourceRegistry.Register(DocumentsActivitySource.Name);
        services.TryAddSingleton<DocumentsMetrics>();

        // T6.1 — wire the Documents → Taxonomy cross-module link. AddGranitTaxonomy
        // is idempotent (TryAdd internally) so calling it here just guarantees the
        // registry is present even when the host forgets to call it explicitly.
        // AddTaggableEntity<Document> registers the closed-generic T5.1 cleanup
        // handler for EntityDeletedEvent<Document>, so trash / permanent-delete
        // drops every TagAssignment + CategoryAssignment row pointing at the
        // document.
        services.AddGranitTaxonomy();
        services.AddTaggableEntity<Document>(scope: "documents");

        // F11 — declarative definitions (ADR-020 placement: base module).
        services.AddQueryDefinition<Document, DocumentQueryDefinition>();
        services.AddQueryDefinition<Folder, FolderQueryDefinition>();
        services.AddQueryDefinition<TenantStorageQuota, TenantStorageQuotaQueryDefinition>();

        services.AddExportDefinition<Document, DocumentExportDefinition>();
        services.AddExportDefinition<Folder, FolderExportDefinition>();
        services.AddExportDefinition<TenantStorageQuota, TenantStorageQuotaExportDefinition>();

        services.AddMetricDefinition<Document, int, DocumentCountMetricDefinition>();
        services.AddMetricDefinition<Folder, int, FolderCountMetricDefinition>();
        services.AddMetricDefinition<TenantStorageQuota, long, TotalStorageUsedMetricDefinition>();

        OptionsBuilder<GranitDocumentsOptions> optionsBuilder = services
            .AddOptions<GranitDocumentsOptions>()
            .BindConfiguration(GranitDocumentsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services;
    }
}
