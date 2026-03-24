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
            "Granit.Domain.AggregateRoot",
            "Granit.Domain.CreationAuditedAggregateRoot",
            "Granit.Domain.AuditedAggregateRoot",
            "Granit.Domain.FullAuditedAggregateRoot",
        ];

        return architecture.Classes
            .Where(c => c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && !aggregateRootBases.Contains(c.FullName)
                && !allowed.Contains(c.FullName)
                && c.Dependencies.Any(d =>
                    d.Target.FullName == "Granit.Events.IDomainEventSource"
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

    /// <summary>
    /// Aggregate root subclasses must have a <c>public static</c> factory method named <c>Create</c>.
    /// This enforces controlled creation and prevents direct <c>new</c> usage.
    /// </summary>
    public static void AggregateRootsShouldHaveFactoryMethod(
        Architecture architecture,
        string typePrefix)
    {
        List<string> violations = [];

        foreach (Class c in architecture.Classes
            .Where(c => c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && !c.IsAbstract.GetValueOrDefault()
                && IsAssignableToAggregateRoot(c)))
        {
            // ArchUnitNET MethodMember.Name includes parameter types (e.g. "Create(Guid,String)").
            bool hasCreate = c.Members.OfType<MethodMember>()
                .Any(m => m.Name.StartsWith("Create(", StringComparison.Ordinal)
                    && m.Visibility == Visibility.Public);

            if (!hasCreate)
            {
                violations.Add(c.FullName);
            }
        }

        violations.ShouldBeEmpty(
            "Aggregate roots must have a public static Create(...) factory method. " +
            "Direct construction via 'new' is not allowed. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// Aggregate root subclasses must have a private parameterless constructor for EF Core
    /// materialization. Without it, EF Core cannot hydrate entities from the database.
    /// </summary>
    public static void AggregateRootsShouldHavePrivateParameterlessConstructor(
        Architecture architecture,
        string typePrefix)
    {
        List<string> violations = [];

        foreach (Class c in architecture.Classes
            .Where(c => c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && !c.IsAbstract.GetValueOrDefault()
                && IsAssignableToAggregateRoot(c)))
        {
            // ArchUnitNET may not expose constructors in Members. Check via
            // the type's Constructors property or fall back to member scan.
            bool hasPrivateCtor = c.Constructors
                .Any(ctor => ctor.Visibility == Visibility.Private
                    && !ctor.Parameters.Any());

            if (!hasPrivateCtor)
            {
                violations.Add(c.FullName);
            }
        }

        violations.ShouldBeEmpty(
            "Aggregate roots must have a private parameterless constructor for EF Core materialization. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// Enforces event naming conventions:
    /// <list type="bullet">
    /// <item><c>IDomainEvent</c> implementors must end with <c>Event</c></item>
    /// <item><c>IIntegrationEvent</c> implementors must end with <c>Eto</c></item>
    /// </list>
    /// Generic lifecycle events (<c>EntityCreatedEvent&lt;T&gt;</c>, <c>EntityCreatedEto&lt;T&gt;</c>) are excluded.
    /// </summary>
    public static void EventNamingShouldFollowConvention(
        Architecture architecture,
        string typePrefix)
    {
        List<string> violations = [];

        foreach (Class c in architecture.Classes
            .Where(c => c.FullName.StartsWith(typePrefix, StringComparison.Ordinal)
                && !c.IsAbstract.GetValueOrDefault()))
        {
            // Skip generic lifecycle events (e.g. EntityCreatedEvent`1)
            if (c.Name.Contains('`'))
            {
                continue;
            }

            bool implementsDomainEvent = c.Dependencies.Any(d =>
                d.Target.FullName == "Granit.Events.IDomainEvent"
                && d is ArchUnitNET.Domain.Dependencies.ImplementsInterfaceDependency);

            bool implementsIntegrationEvent = c.Dependencies.Any(d =>
                d.Target.FullName == "Granit.Events.IIntegrationEvent"
                && d is ArchUnitNET.Domain.Dependencies.ImplementsInterfaceDependency);

            if (implementsDomainEvent && !c.Name.EndsWith("Event", StringComparison.Ordinal))
            {
                violations.Add($"{c.FullName} (IDomainEvent must end with 'Event')");
            }

            if (implementsIntegrationEvent && !c.Name.EndsWith("Eto", StringComparison.Ordinal))
            {
                violations.Add($"{c.FullName} (IIntegrationEvent must end with 'Eto')");
            }
        }

        violations.ShouldBeEmpty(
            "Event naming convention violated — IDomainEvent → *Event, IIntegrationEvent → *Eto. " +
            $"Violators: {string.Join(", ", violations)}");
    }

    private static bool IsAssignableToValueObject(Class c) =>
        HasBaseClass(c, "Granit.Domain.ValueObject");

    private static bool IsAssignableToEntity(Class c) =>
        HasBaseClass(c, "Granit.Domain.Entity");

    private static bool IsAssignableToAggregateRoot(Class c) =>
        HasBaseClass(c, "Granit.Domain.AggregateRoot")
        || HasBaseClass(c, "Granit.Domain.CreationAuditedAggregateRoot")
        || HasBaseClass(c, "Granit.Domain.AuditedAggregateRoot")
        || HasBaseClass(c, "Granit.Domain.FullAuditedAggregateRoot");

    private static bool HasBaseClass(Class c, string baseFullName) =>
        c.Dependencies.Any(d =>
            d is ArchUnitNET.Domain.Dependencies.InheritsBaseClassDependency
            && (d.Target.FullName == baseFullName
                || (d.Target is Class baseClass && HasBaseClass(baseClass, baseFullName))));
}
