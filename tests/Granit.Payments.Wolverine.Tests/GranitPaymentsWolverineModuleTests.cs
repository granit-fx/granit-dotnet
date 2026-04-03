using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Payments.Wolverine.Tests;

public sealed class GranitPaymentsWolverineModuleTests
{
    [Fact]
    public void Module_ShouldBeSealed()
    {
        typeof(GranitPaymentsWolverineModule).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void Module_ShouldInheritFromGranitModule()
    {
        typeof(GranitPaymentsWolverineModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();
    }

    [Fact]
    public void Module_ShouldDependOnPaymentsAndWolverine()
    {
        DependsOnAttribute[] attributes = typeof(GranitPaymentsWolverineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldNotBeEmpty();

        Type[] dependentTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();
        dependentTypes.ShouldContain(typeof(GranitPaymentsModule));
    }

    [Fact]
    public void Module_CanBeInstantiated()
    {
        var module = new GranitPaymentsWolverineModule();
        module.ShouldNotBeNull();
    }
}
