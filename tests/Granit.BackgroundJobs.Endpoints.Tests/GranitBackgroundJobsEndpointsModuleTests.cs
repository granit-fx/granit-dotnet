using Granit.Authorization;
using Granit.BackgroundJobs.Endpoints;
using Granit.Core.Modularity;
using Granit.Querying;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Endpoints.Tests;

public sealed class GranitBackgroundJobsEndpointsModuleTests
{
    [Fact]
    public void DependsOn_DeclaresRequiredModules()
    {
        DependsOnAttribute? attribute = typeof(GranitBackgroundJobsEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .OfType<DependsOnAttribute>()
            .SingleOrDefault();

        attribute.ShouldNotBeNull();
        attribute!.DependedTypes.ShouldContain(typeof(GranitAuthorizationModule));
        attribute.DependedTypes.ShouldContain(typeof(GranitBackgroundJobsModule));
        attribute.DependedTypes.ShouldContain(typeof(GranitQueryingModule));
    }

    [Fact]
    public void Module_IsGranitModule()
    {
        GranitBackgroundJobsEndpointsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
