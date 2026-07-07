using Granit.Guids;
using Granit.Modularity;
using Granit.QueryEngine;
using Granit.Timing;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class GranitTimelineModuleTests
{
    [Fact]
    public void Module_DependsOn_GranitGuidsModule()
    {
        DependsOnAttribute[] attributes = [.. typeof(GranitTimelineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()];

        Type[] allDeps = [.. attributes.SelectMany(a => a.DependedTypes)];
        allDeps.ShouldContain(typeof(GranitGuidsModule));
    }

    [Fact]
    public void Module_DependsOn_GranitQueryEngineModule()
    {
        DependsOnAttribute[] attributes = [.. typeof(GranitTimelineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()];

        Type[] allDeps = [.. attributes.SelectMany(a => a.DependedTypes)];
        allDeps.ShouldContain(typeof(GranitQueryEngineModule));
    }

    [Fact]
    public void Module_DependsOn_removed_security_dependency()
    {
        DependsOnAttribute[] attributes = [.. typeof(GranitTimelineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()];

        Type[] allDeps = [.. attributes.SelectMany(a => a.DependedTypes)];

        allDeps.ShouldNotContain(t => t.Name == "GranitSecurityModule",
            "GranitSecurityModule was dissolved — it should no longer appear in DependsOn");
    }

    [Fact]
    public void Module_DependsOn_GranitTimingModule()
    {
        DependsOnAttribute[] attributes = [.. typeof(GranitTimelineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()];

        Type[] allDeps = [.. attributes.SelectMany(a => a.DependedTypes)];
        allDeps.ShouldContain(typeof(GranitTimingModule));
    }

    [Fact]
    public void Module_IsSealed() => typeof(GranitTimelineModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() => typeof(GranitTimelineModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();
}
