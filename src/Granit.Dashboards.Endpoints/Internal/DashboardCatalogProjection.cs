using Granit.Dashboards.Endpoints.Dtos;

namespace Granit.Dashboards.Endpoints.Internal;

/// <summary>
/// Maps <see cref="IDashboardDefinitionDescriptor"/> instances to the wire shape
/// (<see cref="DashboardCatalogEntryResponse"/>) and applies the
/// <c>?category=</c> filter. Extracted to its own class so the projection logic
/// is unit-testable without the full WebApplication harness.
/// </summary>
internal static class DashboardCatalogProjection
{
    public static IReadOnlyList<DashboardCatalogEntryResponse> Project(
        IEnumerable<IDashboardDefinitionDescriptor> descriptors,
        DashboardCategory? category)
    {
        IEnumerable<IDashboardDefinitionDescriptor> filtered = category.HasValue
            ? descriptors.Where(d => d.Category == category.Value)
            : descriptors;

        return [.. filtered.Select(ToResponse)];
    }

    public static DashboardCatalogEntryResponse ToResponse(IDashboardDefinitionDescriptor d)
    {
        // Multi-view dashboards expose the entry-view's widget count rather than
        // the top-level (which is empty by convention). Falls back to the first
        // view when DefaultView is null.
        int widgetCount = d.Views is { Count: > 0 } views
            ? (FindEntryView(views, d.DefaultView)?.Widgets.Count ?? 0)
            : d.Widgets.Count;

        return new DashboardCatalogEntryResponse(
            Name: d.Name,
            Category: d.Category,
            IsSystem: d.IsSystem,
            Version: d.Version,
            WidgetCount: widgetCount,
            HasViews: d.Views is { Count: > 0 },
            HasAliases: d.Aliases is { Count: > 0 },
            HasFilters: d.Filters is { Count: > 0 });
    }

    private static DashboardView? FindEntryView(IReadOnlyList<DashboardView> views, string? defaultView)
        => defaultView is null
            ? views.Count > 0 ? views[0] : null
            : views.FirstOrDefault(v => v.Name == defaultView) ?? (views.Count > 0 ? views[0] : null);
}
