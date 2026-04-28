using System.Reflection;
using Granit.Analytics.Dashboards.Widgets;
using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces story #1382 (B1): every widget reference inside a shipped
/// <see cref="DashboardDefinition"/> MUST resolve at composition time.
/// <see cref="KpiWidgetDefinition.MetricName"/> must match a registered
/// <see cref="MetricDefinition{TEntity, TValue}"/>; the <c>QueryName</c> on
/// chart / table / pivot widgets must match a registered
/// <see cref="QueryDefinition{TEntity}"/>. Presentation-only widgets
/// (<c>Markdown</c>, <c>Image</c>, <c>Text</c>) are not data-bound and skipped.
/// </summary>
/// <remarks>
/// This catches the "dashboard shipped, metric was renamed, nobody noticed" failure
/// mode at build time rather than at the first user-clicks-import in production.
/// The test only enforces references for dashboards actually shipped by the
/// framework — any new module that adds a dashboard automatically gets the check
/// for free.
/// </remarks>
public sealed class DashboardWidgetReferenceTests
{
    [Fact]
    public void Every_widget_reference_should_resolve_to_a_registered_metric_or_query()
    {
        Inventory inventory = ScanInventory();

        if (inventory.Dashboards.Count == 0)
        {
            // No dashboards shipped yet — story B1 lands the abstractions; concrete
            // dashboards arrive in follow-up stories. The test stays green and
            // becomes load-bearing the moment a module ships its first dashboard.
            return;
        }

        List<string> dangling = [];

        foreach (DashboardDefinition dashboard in inventory.Dashboards)
        {
            foreach (WidgetDefinition widget in dashboard.Widgets)
            {
                switch (widget)
                {
                    case KpiWidgetDefinition kpi
                        when !inventory.MetricNames.Contains(kpi.MetricName):
                        dangling.Add($"{dashboard.Name}/{kpi.Slug} -> KPI references missing metric '{kpi.MetricName}'");
                        break;

                    case ChartWidgetDefinition chart
                        when !inventory.QueryNames.Contains(chart.QueryName):
                        dangling.Add($"{dashboard.Name}/{chart.Slug} -> Chart references missing query '{chart.QueryName}'");
                        break;

                    case TableWidgetDefinition table
                        when !inventory.QueryNames.Contains(table.QueryName):
                        dangling.Add($"{dashboard.Name}/{table.Slug} -> Table references missing query '{table.QueryName}'");
                        break;

                    case PivotWidgetDefinition pivot
                        when !inventory.QueryNames.Contains(pivot.QueryName):
                        dangling.Add($"{dashboard.Name}/{pivot.Slug} -> Pivot references missing query '{pivot.QueryName}'");
                        break;
                }
            }
        }

        dangling.ShouldBeEmpty(
            "Story #1382 (B1): every widget in a shipped DashboardDefinition must reference " +
            "an existing MetricDefinition (KPI) or QueryDefinition (Chart, Table, Pivot). " +
            "Dangling references almost always mean a metric / query was renamed without " +
            "updating the dashboards that depended on it. Either fix the reference or remove " +
            "the widget from the dashboard." + Environment.NewLine +
            string.Join(Environment.NewLine, dangling.Order(StringComparer.Ordinal)));
    }

    private static Inventory ScanInventory()
    {
        string outputDir = Path.GetDirectoryName(typeof(DashboardWidgetReferenceTests).Assembly.Location)!;

        Assembly[] assemblies = Directory.GetFiles(outputDir, "Granit.*.dll")
            .Where(path =>
            {
                string name = Path.GetFileNameWithoutExtension(path);
                return !name.Contains("Tests", StringComparison.Ordinal)
                    && !name.EndsWith(".resources", StringComparison.Ordinal);
            })
            .Select(path =>
            {
                try { return Assembly.LoadFrom(path); }
                catch (Exception ex) when (ex is BadImageFormatException or FileLoadException) { return null; }
            })
            .Where(a => a is not null)
            .ToArray()!;

        List<DashboardDefinition> dashboards = [];
        HashSet<string> metricNames = new(StringComparer.Ordinal);
        HashSet<string> queryNames = new(StringComparer.Ordinal);

        foreach (Assembly assembly in assemblies)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = [.. ex.Types.Where(t => t is not null)!]; }

            foreach (Type type in types)
            {
                if (type.IsAbstract || !type.IsClass)
                {
                    continue;
                }

                if (typeof(DashboardDefinition).IsAssignableFrom(type))
                {
                    DashboardDefinition? dashboard = TryCreate<DashboardDefinition>(type);
                    if (dashboard is not null)
                    {
                        dashboards.Add(dashboard);
                    }

                    continue;
                }

                if (DerivesFromOpenGeneric(type, typeof(MetricDefinition<,>)))
                {
                    IMetricDefinitionDescriptor? descriptor = TryCreate<IMetricDefinitionDescriptor>(type);
                    if (descriptor is not null)
                    {
                        metricNames.Add(descriptor.Name);
                    }

                    continue;
                }

                if (DerivesFromOpenGeneric(type, typeof(QueryDefinition<>)))
                {
                    object? instance = TryCreate<object>(type);
                    if (instance is not null)
                    {
                        string? name = (string?)type.GetProperty("Name", BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);
                        if (!string.IsNullOrEmpty(name))
                        {
                            queryNames.Add(name);
                        }
                    }
                }
            }
        }

        return new Inventory(dashboards, metricNames, queryNames);
    }

    private static T? TryCreate<T>(Type type)
        where T : class
    {
        try { return Activator.CreateInstance(type) as T; }
        catch (Exception) { return null; }
    }

    private static bool DerivesFromOpenGeneric(Type candidate, Type openGenericBase)
    {
        for (Type? cursor = candidate.BaseType; cursor is not null; cursor = cursor.BaseType)
        {
            if (cursor.IsGenericType && cursor.GetGenericTypeDefinition() == openGenericBase)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record Inventory(
        List<DashboardDefinition> Dashboards,
        HashSet<string> MetricNames,
        HashSet<string> QueryNames);
}
