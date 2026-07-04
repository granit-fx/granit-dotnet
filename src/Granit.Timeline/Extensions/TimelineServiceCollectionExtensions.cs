using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.QueryEngine.Extensions;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Diagnostics;
using Granit.Timeline.Domain;
using Granit.Timeline.Exports;
using Granit.Timeline.Internal;
using Granit.Timeline.Options;
using Granit.Timeline.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Timeline.Extensions;

/// <summary>
/// Extension methods for registering Granit.Timeline services.
/// </summary>
public static class TimelineServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Granit.Timeline activity stream engine with default in-memory stores.
    /// For production, call <c>AddGranitTimelineEntityFrameworkCore()</c>
    /// to enable durable PostgreSQL persistence.
    /// </summary>
    public static IServiceCollection AddGranitTimeline(this IServiceCollection services)
    {
        services.AddOptions<TimelineOptions>();

        // Diagnostics: the meter is resolved from IMeterFactory, which the host registers via
        // AddMetrics, and the ActivitySource is exported for the host's OpenTelemetry tracer pipeline.
        GranitActivitySourceRegistry.Register(TimelineActivitySource.Name);
        services.TryAddSingleton<TimelineMetrics>();

        // Core stores (default: in-memory, replaced by EF Core package).
        // Scoped: depends on ICurrentUserService (scoped per-request).
        services.TryAddScoped<InMemoryTimelineStore>();
        services.TryAddScoped<ITimelineWriter>(sp => sp.GetRequiredService<InMemoryTimelineStore>());
        services.TryAddScoped<ITimelineReader, InMemoryTimelineQuery>();

        // Follower facade (standalone mode, replaced by Granit.Timeline.Notifications)
        services.TryAddScoped<ITimelineFollowerService, InMemoryTimelineFollowerService>();

        // Notifier facade (no-op, replaced by Granit.Timeline.Notifications)
        services.TryAddScoped<ITimelineNotifier, NullTimelineNotifier>();

        // Reactions (story C1) — default in-memory store, replaced by EF Core.
        // Single instance backs both reader + writer because the in-memory
        // implementation needs a shared list across roles.
        services.TryAddScoped<InMemoryReactionStore>();
        services.TryAddScoped<IReactionReader>(sp => sp.GetRequiredService<InMemoryReactionStore>());
        services.TryAddScoped<IReactionWriter>(sp => sp.GetRequiredService<InMemoryReactionStore>());

        // Query + Export definitions (ADR-020: owned by the base module).
        services.AddQueryDefinition<TimelineEntry, TimelineEntryQueryDefinition>();
        services.AddExportDefinition<TimelineEntry, TimelineEntryExportDefinition>();

        return services;
    }
}
