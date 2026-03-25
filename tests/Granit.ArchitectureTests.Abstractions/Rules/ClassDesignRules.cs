using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable class design rules: sealed DbContexts, internal EfStores, no MVC, sealed Options,
/// internal Configurations, IEntityTypeConfiguration confinement, Internal namespace visibility.
/// </summary>
public static class ClassDesignRules
{
    /// <summary>
    /// All DbContext subclasses must be sealed (isolated DbContext pattern).
    /// </summary>
    public static void DbContextClassesShouldBeSealed(ArchUnitNET.Domain.Architecture architecture)
    {
        IArchRule rule = Classes()
            .That().AreAssignableTo(typeof(DbContext))
            .And().AreNot(typeof(DbContext))
            .Should().BeSealed()
            .Because("isolated DbContext pattern requires sealed DbContexts");

        rule.Check(architecture);
    }

    /// <summary>
    /// Ef*Store implementations must not be public (internal infrastructure detail).
    /// </summary>
    public static void EfStoreImplementationsShouldNotBePublic(ArchUnitNET.Domain.Architecture architecture)
    {
        IArchRule rule = Classes()
            .That().HaveNameStartingWith("Ef")
            .And().HaveNameEndingWith("Store")
            .Should().NotBePublic()
            .Because("EF Core store implementations are internal infrastructure details");

        rule.Check(architecture);
    }

    /// <summary>
    /// No MVC controllers allowed — Minimal API only.
    /// </summary>
    public static void NoMvcControllersAllowed(ArchUnitNET.Domain.Architecture architecture, string typePrefix)
    {
        IEnumerable<Class> controllers = architecture.Classes
            .Where(c => c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && c.Dependencies.Any(d =>
                    d.Target.FullName == "Microsoft.AspNetCore.Mvc.ControllerBase"
                    || d.Target.FullName == "Microsoft.AspNetCore.Mvc.Controller"));

        controllers.ShouldBeEmpty(
            "Minimal API only — no MVC controllers allowed. " +
            $"Violators: {string.Join(", ", controllers.Select(c => c.FullName))}");
    }

    /// <summary>
    /// IEntityTypeConfiguration implementations must reside in persistence layer namespaces.
    /// By default, allows *.EntityFrameworkCore and *.Migrations namespaces.
    /// </summary>
    public static void EntityTypeConfigurationsShouldBeInEfCoreLayer(
        ArchUnitNET.Domain.Architecture architecture,
        string typePrefix,
        params string[] additionalAllowedNamespaceFragments)
    {
        string[] defaultAllowed = ["EntityFrameworkCore", "Migrations", "Database"];
        string[] allAllowed = [.. defaultAllowed, .. additionalAllowedNamespaceFragments];

        IEnumerable<IType> violations = architecture.Types
            .Where(t => t.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && t.Dependencies.Any(d =>
                    d.Target.FullName.StartsWith("Microsoft.EntityFrameworkCore.IEntityTypeConfiguration", StringComparison.Ordinal))
                && !allAllowed.Any(ns =>
                    t.Namespace.FullName.Contains(ns, StringComparison.Ordinal)));

        violations.ShouldBeEmpty(
            "IEntityTypeConfiguration<T> implementations must reside in persistence layer namespaces. " +
            $"Violators: {string.Join(", ", violations.Select(t => t.FullName))}");
    }

    /// <summary>
    /// EF entity configuration classes (names ending with "Configuration" in EntityFrameworkCore namespaces)
    /// must not be public (unless abstract — meant to be subclassed by consumers).
    /// </summary>
    public static void EntityConfigurationsShouldNotBePublic(
        ArchUnitNET.Domain.Architecture architecture,
        string typePrefix)
    {
        IEnumerable<Class> violations = architecture.Classes
            .Where(c => c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && c.Name.EndsWith("Configuration", StringComparison.Ordinal)
                && c.Namespace.FullName.Contains("EntityFrameworkCore", StringComparison.Ordinal)
                && c.IsAbstract != true
                && c.Visibility == Visibility.Public);

        violations.ShouldBeEmpty(
            "EF Core entity configuration classes must be internal (infrastructure detail). " +
            $"Violators: {string.Join(", ", violations.Select(c => c.FullName))}");
    }

    /// <summary>
    /// Public types must not reside in namespaces containing "Internal" — these are implementation details.
    /// Exceptions: abstract classes (intended as extension points) and types in test assemblies.
    /// </summary>
    public static void PublicTypesShouldNotResideInInternalNamespaces(
        ArchUnitNET.Domain.Architecture architecture,
        string typePrefix,
        params string[] excludedTypeFullNames)
    {
        HashSet<string> excluded = excludedTypeFullNames.ToHashSet(StringComparer.Ordinal);

        IEnumerable<IType> violations = architecture.Types
            .Where(t => t.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && (t.Namespace.FullName.Contains(".Internal.", StringComparison.Ordinal)
                    || t.Namespace.FullName.EndsWith(".Internal", StringComparison.Ordinal))
                && t.Visibility == Visibility.Public
                && t is not Class { IsAbstract: true }
                && !excluded.Contains(t.FullName));

        violations.ShouldBeEmpty(
            "Public types must not reside in *.Internal.* namespaces — they are implementation details. " +
            $"Violators: {string.Join(", ", violations.Select(t => t.FullName))}");
    }

    /// <summary>
    /// Concrete exception classes must be sealed (unless they serve as base classes for other exceptions).
    /// Abstract exceptions are excluded. Exception classes that are subclassed by other exceptions
    /// (e.g. BusinessException) are also excluded.
    /// </summary>
    public static void ConcreteExceptionClassesShouldBeSealed(
        ArchUnitNET.Domain.Architecture architecture,
        string typePrefix)
    {
        IEnumerable<Class> exceptionClasses = architecture.Classes
            .Where(c => c.Name.EndsWith("Exception", StringComparison.Ordinal)
                && c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && c.IsAbstract != true);

        // Collect exception classes that are inherited by other exception classes
        var baseExceptionClasses = architecture.Classes
            .Where(c => c.Name.EndsWith("Exception", StringComparison.Ordinal))
            .SelectMany(c => c.Dependencies
                .Where(d => d is ArchUnitNET.Domain.Dependencies.InheritsBaseClassDependency
                    && d.Target.Name.EndsWith("Exception", StringComparison.Ordinal))
                .Select(d => d.Target.FullName))
            .ToHashSet(StringComparer.Ordinal);

        IEnumerable<Class> unsealed = exceptionClasses
            .Where(c => c.IsSealed != true && !baseExceptionClasses.Contains(c.FullName));

        unsealed.ShouldBeEmpty(
            "Concrete exception classes must be sealed (unless they are base classes for other exceptions). " +
            $"Violators: {string.Join(", ", unsealed.Select(c => c.FullName))}");
    }

    /// <summary>
    /// Options classes must be sealed (unless they serve as base classes for other Options).
    /// </summary>
    public static void OptionsClassesShouldBeSealed(ArchUnitNET.Domain.Architecture architecture, string typePrefix)
    {
        IEnumerable<Class> optionsClasses = architecture.Classes
            .Where(c => c.Name.EndsWith("Options", StringComparison.Ordinal)
                && c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && c.IsAbstract != true);

        // Collect names of classes that are inherited by other Options classes
        var baseOptionClasses = architecture.Classes
            .Where(c => c.Name.EndsWith("Options", StringComparison.Ordinal))
            .SelectMany(c => c.Dependencies
                .Where(d => d.Target.Name.EndsWith("Options", StringComparison.Ordinal))
                .Select(d => d.Target.FullName))
            .ToHashSet(StringComparer.Ordinal);

        IEnumerable<Class> unsealed = optionsClasses
            .Where(c => c.IsSealed != true && !baseOptionClasses.Contains(c.FullName));

        unsealed.ShouldBeEmpty(
            "Options classes must be sealed. " +
            $"Violators: {string.Join(", ", unsealed.Select(c => c.FullName))}");
    }
}
