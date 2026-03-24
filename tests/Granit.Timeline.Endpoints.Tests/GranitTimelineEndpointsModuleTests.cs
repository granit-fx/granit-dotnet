using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Endpoints.Tests;

public sealed class GranitTimelineEndpointsModuleTests
{
    [Fact]
    public void Module_IsSealed() => typeof(GranitTimelineEndpointsModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() => typeof(GranitTimelineEndpointsModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitAuthorizationModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitTimelineEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] allDeps = attributes.SelectMany(a => a.DependedTypes).ToArray();
        allDeps.ShouldContain(typeof(GranitAuthorizationModule));
    }

    [Fact]
    public void Module_DependsOn_GranitTimelineModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitTimelineEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] allDeps = attributes.SelectMany(a => a.DependedTypes).ToArray();
        allDeps.ShouldContain(typeof(GranitTimelineModule));
    }

    [Fact]
    public void Module_DependsOn_GranitHttpApiDocumentationModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitTimelineEndpointsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] allDeps = attributes.SelectMany(a => a.DependedTypes).ToArray();
        allDeps.ShouldContain(typeof(GranitHttpApiDocumentationModule));
    }
}
