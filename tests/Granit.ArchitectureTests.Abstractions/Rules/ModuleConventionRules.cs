using System.Reflection;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Shouldly;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable module convention rules: sealed, proper naming, assembly name alignment.
/// </summary>
public static class ModuleConventionRules
{
    /// <summary>
    /// All subclasses of <paramref name="moduleBaseType"/> must be sealed.
    /// </summary>
    public static void ModulesShouldBeSealed(
        ArchUnitNET.Domain.Architecture architecture,
        Type moduleBaseType)
    {
        IArchRule rule = Classes()
            .That().AreAssignableTo(moduleBaseType)
            .And().AreNot(moduleBaseType)
            .Should().BeSealed()
            .Because("all module subclasses must be sealed to prevent inheritance");

        rule.Check(architecture);
    }

    /// <summary>
    /// Module class names must start with <paramref name="namePrefix"/> and end with <paramref name="nameSuffix"/>.
    /// </summary>
    public static void ModuleNamesShouldFollowConvention(
        ArchUnitNET.Domain.Architecture architecture,
        Type moduleBaseType,
        string namePrefix,
        string nameSuffix = "Module")
    {
        IArchRule rule = Classes()
            .That().AreAssignableTo(moduleBaseType)
            .And().AreNot(moduleBaseType)
            .Should().HaveNameStartingWith(namePrefix)
            .AndShould().HaveNameEndingWith(nameSuffix)
            .Because($"module naming convention: {namePrefix}*{nameSuffix}");

        rule.Check(architecture);
    }

    /// <summary>
    /// Each module class name must equal <c>{AssemblyNameWithoutDots}{nameSuffix}</c>.
    /// </summary>
    /// <param name="callerAssembly">The test assembly — used to locate DLLs in the output directory.</param>
    /// <param name="moduleBaseType">The base module type (e.g. <c>typeof(GranitModule)</c>).</param>
    /// <param name="assemblyPrefix">Assembly name prefix to scan (e.g. <c>"Granit."</c>).</param>
    /// <param name="nameSuffix">Module name suffix (default <c>"Module"</c>).</param>
    /// <param name="exemptions">Module type names that intentionally diverge from the convention.</param>
    public static void ModuleClassNameShouldMatchAssemblyName(
        Assembly callerAssembly,
        Type moduleBaseType,
        string assemblyPrefix,
        string nameSuffix = "Module",
        params string[] exemptions)
    {
        HashSet<string> exempt = new(exemptions, StringComparer.Ordinal);

        // Trigger assembly loading by scanning the output directory first
        string outputDir = Path.GetDirectoryName(callerAssembly.Location)!;
        foreach (string dll in Directory.GetFiles(outputDir, $"{assemblyPrefix}*.dll"))
        {
            try { Assembly.LoadFrom(dll); }
            catch { /* best-effort */ }
        }

        List<string> violations = [];

        IEnumerable<Type> moduleTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith(assemblyPrefix, StringComparison.Ordinal) == true)
            .SelectMany(a =>
            {
                try { return a.GetExportedTypes(); }
                catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); }
            })
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && moduleBaseType.IsAssignableFrom(t));

        foreach (Type module in moduleTypes)
        {
            string assemblyName = module.Assembly.GetName().Name!;
            if (!assemblyName.StartsWith(assemblyPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string expected = assemblyName.Replace(".", string.Empty) + nameSuffix;
            if (module.Name == expected || exempt.Contains(module.Name))
            {
                continue;
            }

            violations.Add($"{module.FullName} (assembly {assemblyName}) → expected '{expected}'");
        }

        violations.ShouldBeEmpty(
            $"Module class names must equal '{{AssemblyNameWithoutDots}}{nameSuffix}' " +
            "so [DependsOn], AddModule<>, and OpenAPI tags stay predictable. " +
            "Add to exemptions only when the derived name is unreasonably long. " +
            $"Violations: {string.Join("; ", violations)}");
    }
}
