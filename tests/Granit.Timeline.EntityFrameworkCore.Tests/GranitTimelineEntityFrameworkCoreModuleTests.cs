using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Timeline.EntityFrameworkCore.Tests;

public sealed class GranitTimelineEntityFrameworkCoreModuleTests
{
    [Fact]
    public void Module_IsSealed() => typeof(GranitTimelineEntityFrameworkCoreModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() => typeof(GranitTimelineEntityFrameworkCoreModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitTimelineModule()
    {
        DependsOnAttribute[] attributes = [.. typeof(GranitTimelineEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()];

        Type[] allDeps = [.. attributes.SelectMany(a => a.DependedTypes)];
        allDeps.ShouldContain(typeof(GranitTimelineModule));
    }

    [Fact]
    public void Module_DependsOn_GranitPersistenceEntityFrameworkCoreModule()
    {
        DependsOnAttribute[] attributes = [.. typeof(GranitTimelineEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()];

        Type[] allDeps = [.. attributes.SelectMany(a => a.DependedTypes)];
        allDeps.ShouldContain(typeof(GranitPersistenceEntityFrameworkCoreModule));
    }
}
