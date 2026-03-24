using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Querying.AI.Tests;

public sealed class GranitQueryingAIModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule()
    {
        GranitQueryingAIModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_is_sealed() => typeof(GranitQueryingAIModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_has_DependsOn_for_GranitAIModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitQueryingAIModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
        IEnumerable<Type> dependedTypes = attrs.SelectMany(a => a.DependedTypes);
        dependedTypes.ShouldContain(typeof(Granit.AI.GranitAIModule));
        dependedTypes.ShouldContain(typeof(GranitQueryingModule));
    }
}
