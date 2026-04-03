using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Metering.Wolverine.Tests;

public sealed class MeteringWolverineModuleTests
{
    [Fact]
    public void Module_ShouldBeSealed()
    {
        typeof(GranitMeteringWolverineModule).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void Module_ShouldInheritFromGranitModule()
    {
        typeof(GranitMeteringWolverineModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();
    }

    [Fact]
    public void Module_ShouldDependOnMeteringAndWolverine()
    {
        DependsOnAttribute[] attributes = typeof(GranitMeteringWolverineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldNotBeEmpty();

        Type[] dependentTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();
        dependentTypes.ShouldContain(typeof(GranitMeteringModule));
    }

    [Fact]
    public void Module_CanBeInstantiated()
    {
        var module = new GranitMeteringWolverineModule();
        module.ShouldNotBeNull();
    }
}
