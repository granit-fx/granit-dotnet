using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.Wolverine.Tests;

public sealed class GranitSubscriptionsWolverineModuleTests
{
    [Fact]
    public void Module_ShouldBeSealed()
    {
        typeof(GranitSubscriptionsWolverineModule).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void Module_ShouldInheritFromGranitModule()
    {
        typeof(GranitSubscriptionsWolverineModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();
    }

    [Fact]
    public void Module_ShouldDependOnSubscriptionsAndWolverine()
    {
        DependsOnAttribute[] attributes = typeof(GranitSubscriptionsWolverineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldNotBeEmpty();

        Type[] dependentTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();
        dependentTypes.ShouldContain(typeof(GranitSubscriptionsModule));
    }

    [Fact]
    public void Module_CanBeInstantiated()
    {
        var module = new GranitSubscriptionsWolverineModule();
        module.ShouldNotBeNull();
    }
}
