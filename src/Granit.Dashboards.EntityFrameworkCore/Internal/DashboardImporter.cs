using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Granit.Guids;
using Granit.MultiTenancy;

namespace Granit.Dashboards.EntityFrameworkCore.Internal;

/// <summary>
/// Deep-copies a registered <see cref="DashboardDefinition"/> into a tenant-scoped
/// <see cref="Dashboard"/> aggregate persisted via <see cref="DashboardsDbContext"/>.
/// Per ADR-038 §3 the import freezes the dashboard at the definition's current
/// version — module upgrades surface drift via <see cref="Dashboard.SourceDefinitionVersion"/>
/// rather than retro-editing imported instances.
/// </summary>
internal sealed class DashboardImporter(
    IDashboardDefinitionRegistry registry,
    DashboardsDbContext db,
    IGuidGenerator guidGenerator,
    ICurrentTenant? currentTenant = null)
{
    /// <summary>
    /// Imports the named definition. Returns <c>null</c> when the definition is
    /// not registered (caller translates to 404). Throws on persistence errors.
    /// </summary>
    public async Task<Dashboard?> ImportAsync(string definitionName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionName);

        IDashboardDefinitionDescriptor? def = registry.Find(definitionName);
        if (def is null)
        {
            return null;
        }

        IReadOnlyList<WidgetDefinition> widgets = ResolveEntryWidgets(def);

        var dashboard = Dashboard.Create(
            id: guidGenerator.Create(),
            name: def.Name,
            category: def.Category,
            layoutColumns: def.Layout.Columns,
            layoutRowHeight: def.Layout.RowHeight,
            tenantId: currentTenant?.Id,
            sourceDefinitionName: def.Name,
            sourceDefinitionVersion: def.Version,
            isSystem: def.IsSystem);

        foreach (WidgetDefinition widget in widgets)
        {
            WidgetDefinitionToInstanceMapper.Mapping mapping =
                WidgetDefinitionToInstanceMapper.Map(widget);

            dashboard.AddWidget(
                widgetId: guidGenerator.Create(),
                widgetType: mapping.WidgetType,
                position: widget.Position,
                width: widget.Size.Width,
                height: widget.Size.Height,
                titleLocalizationKey: $"Widget:{def.Name}.{widget.Slug}",
                configJson: mapping.ConfigJson,
                metricName: mapping.MetricName,
                queryName: mapping.QueryName,
                requiredPermission: widget.RequiredPermission);
        }

        db.Dashboards.Add(dashboard);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return dashboard;
    }

    private static IReadOnlyList<WidgetDefinition> ResolveEntryWidgets(IDashboardDefinitionDescriptor def)
    {
        // Multi-view dashboards expose the widgets of the named DefaultView (or the
        // first view when DefaultView is null). Single-view dashboards use the
        // top-level Widgets directly.
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
