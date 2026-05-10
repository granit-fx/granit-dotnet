using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Granit.Modularity;
using Shouldly;
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

    /// <summary>
    /// Names that intentionally diverge from the assembly-derived form (too long otherwise).
    /// Each entry MUST carry an inline justification.
    /// </summary>
    private static readonly HashSet<string> ModuleNameExemptions = new(StringComparer.Ordinal)
    {
        // "AzureCommunicationServices" → "Acs": full name is too long for ergonomic use.
        "GranitNotificationsSmsAcsModule",
    };

    [Fact]
    public void Module_class_name_should_match_assembly_name()
    {
        List<string> violations = [];

        // Architecture loading (static ctor of GranitArchitecture) has already pulled
        // every Granit.*.dll into the AppDomain via Assembly.LoadFrom.
        _ = Architecture;

        IEnumerable<Type> moduleTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Granit.", StringComparison.Ordinal) == true)
            .SelectMany(a =>
            {
                try { return a.GetExportedTypes(); }
                catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); }
            })
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        typeof(GranitModule).IsAssignableFrom(t));

        foreach (Type module in moduleTypes)
        {
            string assemblyName = module.Assembly.GetName().Name!;
            if (!assemblyName.StartsWith("Granit.", StringComparison.Ordinal))
            {
                continue;
            }

            string expected = assemblyName.Replace(".", string.Empty) + "Module";
            if (module.Name == expected || ModuleNameExemptions.Contains(module.Name))
            {
                continue;
            }

            violations.Add($"{module.FullName} (assembly {assemblyName}) → expected '{expected}'");
        }

        violations.ShouldBeEmpty(
            "GranitModule class names must equal '<AssemblyNameWithoutDots>Module' " +
            "so [DependsOn], AddModule<>, and OpenAPI tags stay predictable. " +
            "Add to ModuleNameExemptions only when the derived name is unreasonably long.");
    }
}
