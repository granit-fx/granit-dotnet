using Granit.Analytics;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Rendering;

namespace Granit.Dashboards.Endpoints.Internal;

/// <summary>
/// Translates the rendering-pipeline <see cref="DashboardRenderResult"/> into
/// the HTTP-shape <see cref="DashboardRenderResponse"/>. Lives next to the
/// other projection helpers so the endpoint handler stays reflection-free and
/// the wire shape can be unit-tested without an HTTP harness.
/// </summary>
internal static class DashboardRenderProjection
{
    /// <summary>
    /// Composes the wire response from the renderer's pipeline result, the
    /// persisted <see cref="Dashboard"/> aggregate (source of structural metadata
    /// — <c>Position</c>, <c>Width</c>, <c>Height</c>, <c>TitleLocalizationKey</c>,
    /// <c>RequiredPermission</c>), and an <see cref="IDashboardDefinitionRegistry"/>
    /// look-up that surfaces the declarative <see cref="WidgetAction"/> list from
    /// the source <see cref="WidgetDefinition"/>.
    /// </summary>
    /// <remarks>
    /// Actions are looked up by <see cref="Dashboard.SourceDefinitionName"/> +
    /// per-widget slug — when the source definition is no longer registered (or
    /// the dashboard was custom-built without a source definition),
    /// <see cref="DashboardRenderedWidgetResponse.Actions"/> is <see langword="null"/>.
    /// Definition / persisted-aggregate version drift is intentionally accepted
    /// for v1: the look-up uses the latest registered version even when
    /// <see cref="Dashboard.SourceDefinitionVersion"/> differs. See ADR-038
    /// "drift detection" deferred work.
    /// </remarks>
    public static DashboardRenderResponse ToResponse(
        DashboardRenderResult result,
        Dashboard dashboard,
        IDashboardDefinitionRegistry definitionRegistry,
        string? periodToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(dashboard);
        ArgumentNullException.ThrowIfNull(definitionRegistry);

        DashboardRenderPeriodResponse? period = result.Period is { } p
            ? new DashboardRenderPeriodResponse(p.From, p.To, periodToken)
            : null;

        Dictionary<string, IReadOnlyList<WidgetAction>?> actionsBySlug =
            ResolveActionsBySlug(dashboard, definitionRegistry);

        var instanceById = dashboard.Widgets.ToDictionary(w => w.Id);

        DashboardRenderedWidgetResponse[] widgets = [.. result.Widgets.Select(rw =>
        {
            WidgetInstance instance = instanceById[rw.WidgetId];
            string slug = ExtractSlug(instance.TitleLocalizationKey, dashboard.SourceDefinitionName);
            actionsBySlug.TryGetValue(slug, out IReadOnlyList<WidgetAction>? actions);

            return new DashboardRenderedWidgetResponse(
                Id: rw.WidgetId,
                WidgetType: rw.Envelope.WidgetType,
                Slug: slug,
                Position: instance.Position,
                Width: instance.Width,
                Height: instance.Height,
                TitleLocalizationKey: instance.TitleLocalizationKey,
                Actions: actions,
                RequiredPermission: instance.RequiredPermission,
                Status: rw.Envelope.Status,
                Sequence: rw.Envelope.Sequence,
                EmittedAt: rw.Envelope.EmittedAt,
                RefreshHint: rw.Envelope.RefreshHint,
                Snapshot: rw.Envelope.Snapshot,
                ReasonLocalizationKey: rw.Envelope.ReasonLocalizationKey);
        })];

        return new DashboardRenderResponse(
            DashboardId: result.DashboardId,
            RenderedAt: result.RenderedAt,
            Period: period,
            Widgets: widgets);
    }

    public static ResolvedPeriod? TryBuildResolvedPeriod(DashboardRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PeriodFrom is { } from && request.PeriodTo is { } to)
        {
            return new ResolvedPeriod(from, to);
        }

        return null;
    }

    private static Dictionary<string, IReadOnlyList<WidgetAction>?> ResolveActionsBySlug(
        Dashboard dashboard,
        IDashboardDefinitionRegistry registry)
    {
        Dictionary<string, IReadOnlyList<WidgetAction>?> map = new(StringComparer.Ordinal);

        if (dashboard.SourceDefinitionName is not { } sourceName)
        {
            return map;
        }

        IDashboardDefinitionDescriptor? descriptor = registry.Find(sourceName);
        if (descriptor is null)
        {
            return map;
        }

        // A dashboard's persisted widgets came from one of: (a) the descriptor's
        // top-level Widgets pool, or (b) one named DashboardView's pool. Walk
        // every reachable pool and project Slug -> Actions; later pools win on
        // duplicate slugs (definition authors shouldn't reuse slugs across views,
        // but this never throws if they do).
        AddPool(map, descriptor.Widgets);
        if (descriptor.Views is { } views)
        {
            foreach (DashboardView view in views)
            {
                AddPool(map, view.Widgets);
            }
        }

        return map;

        static void AddPool(
            Dictionary<string, IReadOnlyList<WidgetAction>?> map,
            IReadOnlyList<WidgetDefinition> pool)
        {
            foreach (WidgetDefinition widget in pool)
            {
                map[widget.Slug] = widget.Actions;
            }
        }
    }

    /// <summary>
    /// Extracts the per-widget slug from <c>TitleLocalizationKey</c> by stripping
    /// the <c>"Widget:{SourceDefinitionName}."</c> prefix when present, otherwise
    /// stripping just <c>"Widget:"</c>. The frontend uses the slug to address
    /// the widget in its <c>WidgetAction</c> dispatcher.
    /// </summary>
    private static string ExtractSlug(string titleLocalizationKey, string? sourceDefinitionName)
    {
        if (sourceDefinitionName is { Length: > 0 })
        {
            string prefix = $"Widget:{sourceDefinitionName}.";
            if (titleLocalizationKey.StartsWith(prefix, StringComparison.Ordinal))
            {
                return titleLocalizationKey[prefix.Length..];
            }
        }

        const string GenericPrefix = "Widget:";
        if (titleLocalizationKey.StartsWith(GenericPrefix, StringComparison.Ordinal))
        {
            return titleLocalizationKey[GenericPrefix.Length..];
        }

        return titleLocalizationKey;
    }
}
