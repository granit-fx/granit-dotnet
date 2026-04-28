namespace Granit.Dashboards;

/// <summary>
/// Type-erased descriptor exposed for registry lookup. Lets the registry surface every
/// registered <see cref="DashboardDefinition"/> without each consumer needing the
/// concrete type.
/// </summary>
public interface IDashboardDefinitionDescriptor
{
    /// <summary>Unique wire identifier, e.g. <c>"Granit.Invoicing.FinanceOverview"</c>.</summary>
    string Name { get; }

    /// <summary>Coarse grouping driving catalogue ordering.</summary>
    DashboardCategory Category { get; }

    /// <summary>Whether the dashboard is mandated by the platform operator (cannot be deleted by tenant admins).</summary>
    bool IsSystem { get; }

    /// <summary>Semver — used for drift detection between definition and persisted dashboard.</summary>
    string Version { get; }

    /// <summary>Layout configuration applied to the widget grid.</summary>
    DashboardLayout Layout { get; }

    /// <summary>Widgets shipped by this dashboard, in declared order.</summary>
    IReadOnlyList<WidgetDefinition> Widgets { get; }
}
