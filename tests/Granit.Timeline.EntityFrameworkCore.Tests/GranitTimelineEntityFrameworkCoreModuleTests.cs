using Granit.Core.Modularity;
using Granit.Persistence;
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
        DependsOnAttribute[] attributes = typeof(GranitTimelineEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] allDeps = attributes.SelectMany(a => a.DependedTypes).ToArray();
        allDeps.ShouldContain(typeof(GranitTimelineModule));
    }

    [Fact]
    public void Module_DependsOn_GranitPersistenceModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitTimelineEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] allDeps = attributes.SelectMany(a => a.DependedTypes).ToArray();
        allDeps.ShouldContain(typeof(GranitPersistenceModule));
    }
}
