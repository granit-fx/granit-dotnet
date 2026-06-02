using Granit.ArchitectureTests.Abstractions.Rules;
using Granit.Modularity;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates conventions for GranitModule subclasses.
/// Logic lives in <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos can reuse it.
/// </summary>
public sealed class ModuleConventionTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = GranitArchitecture.Instance;

    [Fact]
    public void Modules_should_be_sealed() =>
        ModuleConventionRules.ModulesShouldBeSealed(Architecture, typeof(GranitModule));

    [Fact]
    public void Module_class_names_should_start_with_Granit_and_end_with_Module() =>
        ModuleConventionRules.ModuleNamesShouldFollowConvention(Architecture, typeof(GranitModule), "Granit", "Module");

    [Fact]
    public void Module_class_name_should_match_assembly_name() =>
        ModuleConventionRules.ModuleClassNameShouldMatchAssemblyName(
            typeof(ModuleConventionTests).Assembly,
            typeof(GranitModule),
            "Granit.",
            "Module",
            // "AzureCommunicationServices" → "Acs": full name is too long for ergonomic use.
            "GranitNotificationsSmsAcsModule");
}
