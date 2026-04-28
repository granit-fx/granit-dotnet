using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests;

/// <summary>
/// Confirms <see cref="DashboardDefinition.Filters"/> default behaviour and that a
/// concrete dashboard can override it to ship dashboard-scoped filter sets.
/// </summary>
public sealed class DashboardDefinitionFiltersTests
{
    [Fact]
    public void Default_ReturnsNull()
        => new EmptyDashboard().Filters.ShouldBeNull();

    [Fact]
    public void Override_SurfacesFiltersOnTheDescriptor()
    {
        IDashboardDefinitionDescriptor d = new FilteredDashboard();

        d.Filters.ShouldNotBeNull();
        d.Filters.Count.ShouldBe(2);
        d.Filters.ShouldContain(f => f.Name == "CurrentCustomer" && !f.Editable);
        d.Filters.ShouldContain(f => f.Name == "DateRange" && f.Editable);
    }

    private sealed class EmptyDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Empty";
        public override DashboardCategory Category => DashboardCategory.General;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [];
    }

    private sealed class FilteredDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Filtered";
        public override DashboardCategory Category => DashboardCategory.Finance;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [];

        public override IReadOnlyList<DashboardFilter>? Filters { get; } =
        [
            new DashboardFilter(
                "CurrentCustomer",
                "Dashboard:Sample.Filtered.Filters.Customer",
                [new DashboardFilterClause("customer.id", DashboardFilterOperator.Eq, "${entityId}")]),
            new DashboardFilter(
                "DateRange",
                "Dashboard:Sample.Filtered.Filters.DateRange",
                [
                    new DashboardFilterClause("issuedAt", DashboardFilterOperator.Gte, "2026-01-01"),
                    new DashboardFilterClause("issuedAt", DashboardFilterOperator.Lt, "2027-01-01"),
                ],
                Editable: true),
        ];
    }
}
