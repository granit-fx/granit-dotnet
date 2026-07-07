using Granit.Modularity;
using Shouldly;

namespace Granit.QueryEngine.AI.Tests;

public sealed class GranitQueryEngineAIModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule()
    {
        GranitQueryEngineAIModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_is_sealed() => typeof(GranitQueryEngineAIModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_has_DependsOn_for_GranitAIModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitQueryEngineAIModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
        IEnumerable<Type> dependedTypes = attrs.SelectMany(a => a.DependedTypes);
        dependedTypes.ShouldContain(typeof(Granit.AI.GranitAIModule));
        dependedTypes.ShouldContain(typeof(GranitQueryEngineModule));
    }
}
