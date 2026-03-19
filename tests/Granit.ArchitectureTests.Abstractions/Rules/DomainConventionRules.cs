using ArchUnitNET.Domain;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable DDD domain convention rules: ValueObject immutability, aggregate root encapsulation,
/// manual IDomainEventSource detection.
/// </summary>
public static class DomainConventionRules
{
    /// <summary>
    /// All concrete <c>ValueObject</c> subclasses must be sealed — value objects have no
    /// further specialization.
    /// </summary>
    public static void ValueObjectSubclassesShouldBeSealed(
        Architecture architecture,
        string typePrefix)
    {
        IEnumerable<Class> violations = architecture.Classes
            .Where(c => c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && !c.IsAbstract.GetValueOrDefault()
                && IsAssignableToValueObject(c)
                && c.IsSealed != true);

        violations.ShouldBeEmpty(
            "Concrete ValueObject subclasses must be sealed — value objects have no further specialization. " +
            $"Violators: {string.Join(", ", violations.Select(c => c.FullName))}");
    }

    /// <summary>
    /// Detects types that manually implement <c>IDomainEventSource</c> instead of inheriting
    /// from an aggregate root base class. These types should be migrated to use
    /// <c>AggregateRoot</c>, <c>AuditedAggregateRoot</c>, or similar.
    /// </summary>
    /// <remarks>
    /// Returns the violating type names rather than asserting, so callers can maintain
    /// an allowlist during the migration period.
    /// </remarks>
    public static IReadOnlyList<string> FindManualDomainEventSourceImplementors(
        Architecture architecture,
        string typePrefix,
        params string[] allowedTypeFullNames)
    {
        HashSet<string> allowed = allowedTypeFullNames.ToHashSet(StringComparer.Ordinal);

        // Base classes that legitimately implement IDomainEventSource
        HashSet<string> aggregateRootBases =
        [
            "Granit.Core.Domain.AggregateRoot",
            "Granit.Core.Domain.CreationAuditedAggregateRoot",
            "Granit.Core.Domain.AuditedAggregateRoot",
            "Granit.Core.Domain.FullAuditedAggregateRoot",
        ];

        return architecture.Classes
            .Where(c => c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && !aggregateRootBases.Contains(c.FullName)
                && !allowed.Contains(c.FullName)
                && c.Dependencies.Any(d =>
                    d.Target.FullName == "Granit.Core.Events.IDomainEventSource"
                    && d is ArchUnitNET.Domain.Dependencies.ImplementsInterfaceDependency)
                && !IsAssignableToAggregateRoot(c))
            .Select(c => c.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Domain entities residing in a <c>Domain/</c> folder should not be in an
    /// <c>Internal</c> namespace — domain types are public contracts.
    /// </summary>
    public static void DomainEntitiesShouldNotBeInternal(
        Architecture architecture,
        string typePrefix)
    {
        IEnumerable<Class> violations = architecture.Classes
            .Where(c => c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && c.Namespace.FullName.Contains(".Domain", StringComparison.Ordinal)
                && (c.Namespace.FullName.Contains(".Internal.", StringComparison.Ordinal)
                    || c.Namespace.FullName.EndsWith(".Internal", StringComparison.Ordinal))
                && (IsAssignableToEntity(c) || IsAssignableToValueObject(c)));

        violations.ShouldBeEmpty(
            "Domain entities and value objects must not reside in Internal namespaces. " +
            $"Violators: {string.Join(", ", violations.Select(c => c.FullName))}");
    }

    /// <summary>
    /// Aggregate root subclasses must not have public mutable properties (except explicit
    /// interface implementations like <c>IMultiTenant.TenantId</c> or <c>ISoftDeletable</c>).
    /// </summary>
    public static void AggregateRootsShouldNotHavePublicSetters(
        Architecture architecture,
        string typePrefix)
    {
        List<string> violations = [];

        foreach (Class c in architecture.Classes
            .Where(c => c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && !c.IsAbstract.GetValueOrDefault()
                && IsAssignableToAggregateRoot(c)))
        {
            // set_ methods with Public visibility indicate mutable public properties.
            // Exclude explicit interface implementations (contain '.' in name).
            violations.AddRange(c.Members
                .Where(m => m.Name.StartsWith("set_", StringComparison.Ordinal)
                    && m.Visibility == Visibility.Public
                    && !m.Name.Contains('.'))
                .Select(m => $"{c.Name}.{m.Name["set_".Length..]}"));
        }

        violations.ShouldBeEmpty(
            "Aggregate root properties must use private setters. " +
            "Use behavior methods for state transitions. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    private static bool IsAssignableToValueObject(Class c) =>
        HasBaseClass(c, "Granit.Core.Domain.ValueObject");

    private static bool IsAssignableToEntity(Class c) =>
        HasBaseClass(c, "Granit.Core.Domain.Entity");

    private static bool IsAssignableToAggregateRoot(Class c) =>
        HasBaseClass(c, "Granit.Core.Domain.AggregateRoot")
        || HasBaseClass(c, "Granit.Core.Domain.CreationAuditedAggregateRoot");

    private static bool HasBaseClass(Class c, string baseFullName) =>
        c.Dependencies.Any(d =>
            d is ArchUnitNET.Domain.Dependencies.InheritsBaseClassDependency
            && (d.Target.FullName == baseFullName
                || (d.Target is Class baseClass && HasBaseClass(baseClass, baseFullName))));
}
