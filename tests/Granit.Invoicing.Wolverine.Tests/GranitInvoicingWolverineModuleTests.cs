using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.Wolverine.Tests;

public sealed class GranitInvoicingWolverineModuleTests
{
    [Fact]
    public void Module_ShouldBeSealed() =>
        typeof(GranitInvoicingWolverineModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_ShouldInheritFromGranitModule() =>
        typeof(GranitInvoicingWolverineModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void Module_ShouldDependOnInvoicingAndWolverine()
    {
        DependsOnAttribute[] attributes = typeof(GranitInvoicingWolverineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldNotBeEmpty();

        Type[] dependentTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();
        dependentTypes.ShouldContain(typeof(GranitInvoicingModule));
    }

    [Fact]
    public void Module_CanBeInstantiated()
    {
        var module = new GranitInvoicingWolverineModule();
        module.ShouldNotBeNull();
    }
}
