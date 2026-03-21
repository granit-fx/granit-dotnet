using Granit.AI;
using Granit.Core.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Timeline.AI.Tests;

public sealed class GranitTimelineAIModuleTests
{
    [Fact]
    public void Module_IsSealed() => typeof(GranitTimelineAIModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() => typeof(GranitTimelineAIModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitAIModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitTimelineAIModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] allDeps = attributes.SelectMany(a => a.DependedTypes).ToArray();
        allDeps.ShouldContain(typeof(GranitAIModule));
    }

    [Fact]
    public void Module_DependsOn_GranitTimelineModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitTimelineAIModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] allDeps = attributes.SelectMany(a => a.DependedTypes).ToArray();
        allDeps.ShouldContain(typeof(GranitTimelineModule));
    }
}
