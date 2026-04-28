using Granit.Dashboards.Endpoints;
using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests;

public sealed class GranitDashboardsEndpointsModuleTests
{
    [Fact]
    public void Module_IsGranitModule()
        => new GranitDashboardsEndpointsModule().ShouldBeAssignableTo<GranitModule>();
}
