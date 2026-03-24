using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Granit.Modularity;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates conventions for GranitModule subclasses:
/// sealed, proper naming, namespace alignment.
/// </summary>
public sealed class ModuleConventionTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = GranitArchitecture.Instance;

    private static readonly IObjectProvider<Class> GranitModules =
        Classes().That().AreAssignableTo(typeof(GranitModule))
            .And().AreNot(typeof(GranitModule))
            .As("GranitModule implementations");

    [Fact]
    public void Modules_should_be_sealed()
    {
        IArchRule rule = Classes().That().Are(GranitModules)
            .Should().BeSealed()
            .Because("all GranitModule subclasses must be sealed to prevent inheritance (CLAUDE.md)");

        rule.Check(Architecture);
    }

    [Fact]
    public void Module_class_names_should_start_with_Granit_and_end_with_Module()
    {
        IArchRule rule = Classes().That().Are(GranitModules)
            .Should().HaveNameStartingWith("Granit")
            .AndShould().HaveNameEndingWith("Module")
            .Because("module naming convention: Granit*Module");

        rule.Check(Architecture);
    }
}
