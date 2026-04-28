using Granit.Dashboards;
using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests;

public sealed class GranitDashboardsAbstractionsModuleTests
{
    [Fact]
    public void Module_IsGranitModule()
    {
        GranitDashboardsAbstractionsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_HasNoServiceRegistrations()
    {
        // Pure contracts module — exists for [DependsOn] composition only. The DI
        // surface for dashboards lives in the runtime package (Granit.Dashboards).
        GranitDashboardsAbstractionsModule module = new();

        module.ShouldNotBeNull();
    }
}
