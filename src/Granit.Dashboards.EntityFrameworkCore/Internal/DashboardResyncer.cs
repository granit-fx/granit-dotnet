using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Granit.Dashboards.EntityFrameworkCore.Internal;

/// <summary>
/// ADR-038 §3 resync orchestrator. Re-imports the registered
/// <see cref="DashboardDefinition"/> behind a persisted <see cref="Dashboard"/>'s
/// <see cref="Dashboard.SourceDefinitionName"/> and replays the import through
/// <see cref="Dashboard.Resync"/>, which carries per-instance overrides forward
/// via slug-match. Mirrors <see cref="DashboardImporter"/> for the import path.
/// </summary>
internal sealed class DashboardResyncer(
    IDashboardDefinitionRegistry registry,
    DashboardsDbContext db,
    IGuidGenerator guidGenerator)
{
    /// <summary>
    /// Resyncs the dashboard whose persisted id is <paramref name="dashboardId"/>.
    /// Returns one of the discriminated outcomes — the endpoint translates each
    /// into the appropriate HTTP status code.
    /// </summary>
    public async Task<DashboardResyncResult> ResyncAsync(Guid dashboardId, CancellationToken cancellationToken)
    {
        Dashboard? dashboard = await db.Dashboards
            .Include(d => d.Widgets)
            .SingleOrDefaultAsync(d => d.Id == dashboardId, cancellationToken)
            .ConfigureAwait(false);
        if (dashboard is null)
        {
            return DashboardResyncResult.NotFound();
        }

        if (dashboard.SourceDefinitionName is not { Length: > 0 } sourceName)
        {
            return DashboardResyncResult.NotApplicable(
                "Dashboard has no source definition — ad-hoc dashboards never drift and cannot be resynced.");
        }

        IDashboardDefinitionDescriptor? descriptor = registry.Find(sourceName);
        if (descriptor is null)
        {
            return DashboardResyncResult.SourceUnregistered(sourceName);
        }

        IReadOnlyList<WidgetDefinition> entryWidgets = ResolveEntryWidgets(descriptor);
        var inputs = new ResyncWidgetInput[entryWidgets.Count];
        for (int i = 0; i < entryWidgets.Count; i++)
        {
            WidgetDefinition widget = entryWidgets[i];
            WidgetDefinitionToInstanceMapper.Mapping mapping = WidgetDefinitionToInstanceMapper.Map(widget);

            inputs[i] = new ResyncWidgetInput(
                WidgetId: guidGenerator.Create(),
                WidgetType: mapping.WidgetType,
                Position: widget.Position,
                Width: widget.Size.Width,
                Height: widget.Size.Height,
                TitleLocalizationKey: $"Widget:{descriptor.Name}.{widget.Slug}",
                ConfigJson: mapping.ConfigJson,
                MetricName: mapping.MetricName,
                QueryName: mapping.QueryName,
                RequiredPermission: widget.RequiredPermission);
        }

        var previousWidgetIds = dashboard.Widgets.Select(w => w.Id).ToHashSet();

        DashboardResyncSummary summary = dashboard.Resync(
            newSourceDefinitionVersion: descriptor.Version,
            newLayoutColumns: descriptor.Layout.Columns,
            newLayoutRowHeight: descriptor.Layout.RowHeight,
            newIsSystem: descriptor.IsSystem,
            incomingWidgets: inputs);

        // EF Core's change tracker classifies a new entity added to a tracked
        // Modified parent's navigation collection by inspecting the entity's key:
        // a non-empty Guid is read as "existing row, mark Modified", which then
        // surfaces a DbUpdateConcurrencyException at SaveChanges (UPDATE matches
        // 0 rows). Force the freshly-allocated widget rows to Added — they came
        // out of `WidgetInstance.Create` with an `IGuidGenerator` Guid and were
        // never persisted under that id.
        foreach (WidgetInstance widget in dashboard.Widgets)
        {
            if (previousWidgetIds.Contains(widget.Id))
            {
                continue;
            }

            EntityEntry<WidgetInstance> entry = db.Entry(widget);
            if (entry.State == EntityState.Modified || entry.State == EntityState.Detached)
            {
                entry.State = EntityState.Added;
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return DashboardResyncResult.Success(dashboard, summary);
    }

    private static IReadOnlyList<WidgetDefinition> ResolveEntryWidgets(IDashboardDefinitionDescriptor def)
    {
        // Mirrors DashboardImporter — entry view widgets for multi-view definitions,
        // top-level widgets for single-view. Keeps resync semantics identical to the
        // initial import (override carry-over still works on the entry view; views
        // beyond entry are materialised on demand by the renderer and never carry
        // overrides today).
        if (def.Views is { Count: > 0 } views)
        {
            DashboardView entry = def.DefaultView is null
                ? views[0]
                : views.FirstOrDefault(v => v.Name == def.DefaultView) ?? views[0];
            return entry.Widgets;
        }

        return def.Widgets;
    }
}

/// <summary>
/// Discriminated outcome surfaced to the endpoint handler.
/// <see cref="DashboardResyncOutcome.Updated"/> carries the live aggregate and the
/// <see cref="DashboardResyncSummary"/> for the wire response;
/// <see cref="DashboardResyncOutcome.NotFound"/> / <see cref="DashboardResyncOutcome.NotApplicable"/>
/// / <see cref="DashboardResyncOutcome.SourceUnregistered"/> map to 404 / 409 /
/// 409 respectively.
/// </summary>
internal sealed record DashboardResyncResult(
    DashboardResyncOutcome Outcome,
    Dashboard? Dashboard,
    DashboardResyncSummary? Summary,
    string? ConflictReason)
{
    public static DashboardResyncResult Success(Dashboard dashboard, DashboardResyncSummary summary)
        => new(DashboardResyncOutcome.Updated, dashboard, summary, null);

    public static DashboardResyncResult NotFound()
        => new(DashboardResyncOutcome.NotFound, null, null, null);

    public static DashboardResyncResult NotApplicable(string reason)
        => new(DashboardResyncOutcome.NotApplicable, null, null, reason);

    public static DashboardResyncResult SourceUnregistered(string sourceName)
        => new(
            DashboardResyncOutcome.SourceUnregistered,
            null,
            null,
            $"Source definition '{sourceName}' is no longer registered — resync requires the module to be loaded.");
}

internal enum DashboardResyncOutcome
{
    Updated,
    NotFound,
    NotApplicable,
    SourceUnregistered,
}
