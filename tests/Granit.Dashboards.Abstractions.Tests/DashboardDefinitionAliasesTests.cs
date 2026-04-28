using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests;

/// <summary>
/// Confirms <see cref="DashboardDefinition.Aliases"/> default and override paths
/// surface through <see cref="IDashboardDefinitionDescriptor"/>.
/// </summary>
public sealed class DashboardDefinitionAliasesTests
{
    [Fact]
    public void Default_ReturnsNull()
        => new EmptyDashboard().Aliases.ShouldBeNull();

    [Fact]
    public void Override_SurfacesAliasesThroughDescriptor()
    {
        IDashboardDefinitionDescriptor d = new ParameterizedDashboard();

        d.Aliases.ShouldNotBeNull();
        d.Aliases.Count.ShouldBe(2);
        d.Aliases.ShouldContain(a => a.Name == "currentCustomer" && a.EntityType == "Customer"
            && a.Resolver is RouteParamResolver);
        d.Aliases.ShouldContain(a => a.Name == "tenant" && a.Resolver is TenantContextResolver);
    }

    private sealed class EmptyDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Empty";
        public override DashboardCategory Category => DashboardCategory.General;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [];
    }

    private sealed class ParameterizedDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Parameterized";
        public override DashboardCategory Category => DashboardCategory.Operations;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [];

        public override IReadOnlyList<EntityAlias>? Aliases { get; } =
        [
            new EntityAlias("currentCustomer", "Customer", new RouteParamResolver("customerId")),
            new EntityAlias("tenant",          "Tenant",   new TenantContextResolver()),
        ];
    }
}
