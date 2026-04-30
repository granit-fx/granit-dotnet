using Granit.Analytics.Extensions;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Entities.Extensions;
using Granit.Parties.Diagnostics;
using Granit.Parties.Domain;
using Granit.Parties.Entities;
using Granit.Parties.Exports;
using Granit.Parties.Metrics;
using Granit.Parties.Queries;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Parties.Extensions;

/// <summary>Extension methods for registering the Granit parties infrastructure.</summary>
public static class PartiesHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit parties module — diagnostics (metrics + ActivitySource), the
    /// Party admin-grid <c>QueryDefinition</c> + <c>ExportDefinition</c>, the KPI
    /// metric definitions, and any shared singletons. Persistence
    /// (<c>Granit.Parties.EntityFrameworkCore</c>), endpoints, and privacy handlers
    /// register their own services.
    /// </summary>
    public static IHostApplicationBuilder AddGranitParties(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<PartiesMetrics>();
        GranitActivitySourceRegistry.Register(PartiesActivitySource.Name);

        // ADR-020: declarative definitions live with the domain, not the HTTP layer.
        builder.Services.AddQueryDefinition<Party, PartyQueryDefinition>();
        builder.Services.AddExportDefinition<Party, PartyExportDefinition>();

        builder.Services.AddMetricDefinition<Party, int, ActivePartyCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Party, int, PartyCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Party, int, MergedPartyCountMetricDefinition>();

        // ADR-040 §Phase 1.F: declares the Party UI surface (form / detail /
        // list collection) so the showcase host renders it without per-form
        // scaffolding (story #1564).
        builder.Services.AddEntityDefinition<Party, PartyEntityDefinition>();

        return builder;
    }
}
