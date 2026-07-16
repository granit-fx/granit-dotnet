using Granit.ArchitectureTests.Abstractions.Rules;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates DDD domain conventions: ValueObject immutability, aggregate root encapsulation,
/// manual IDomainEventSource detection, Domain/ namespace visibility.
/// </summary>
public sealed class DomainConventionTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = GranitArchitecture.Instance;

    [Fact]
    public void ValueObject_subclasses_should_be_sealed() =>
        DomainConventionRules.ValueObjectSubclassesShouldBeSealed(Architecture, "Granit.");

    [Fact]
    public void Domain_entities_should_not_be_in_Internal_namespaces() =>
        DomainConventionRules.DomainEntitiesShouldNotBeInternal(Architecture, "Granit.");

    [Fact]
    public void Aggregate_roots_should_not_have_public_setters() =>
        DomainConventionRules.AggregateRootsShouldNotHavePublicSetters(Architecture, "Granit.");

    /// <summary>
    /// Reflection complement to <see cref="Aggregate_roots_should_not_have_public_setters"/>: the
    /// ArchUnitNET rule misses a public setter that implicitly implements an interface member (e.g.
    /// an <c>IMultiTenant.TenantId</c> auto-property). Use <c>private set</c> + an explicit interface
    /// implementation instead.
    /// </summary>
    [Fact]
    public void Aggregate_roots_must_not_expose_public_property_setters()
    {
        IReadOnlyList<System.Reflection.Assembly> assemblies =
            Granit.ArchitectureTests.Abstractions.ArchitectureLoader.LoadAssemblies(
                "Granit.", typeof(DomainConventionTests).Assembly);

        IReadOnlyList<string> violations =
            DomainConventionRules.FindAggregateRootsWithPublicPropertySetters(assemblies, "Granit.");

        violations.ShouldBeEmpty(
            "Aggregate root properties must use private setters; expose IMultiTenant.TenantId (and "
            + "similar interface members) via an explicit interface implementation over a private "
            + $"setter. Violators: {string.Join(", ", violations)}");
    }

    [Fact]
    public void Aggregate_roots_should_have_factory_method() =>
        DomainConventionRules.AggregateRootsShouldHaveFactoryMethod(Architecture, "Granit.");

    [Fact]
    public void Aggregate_roots_should_have_private_parameterless_constructor() =>
        DomainConventionRules.AggregateRootsShouldHavePrivateParameterlessConstructor(Architecture, "Granit.");

    [Fact]
    public void Event_naming_should_follow_convention() =>
        DomainConventionRules.EventNamingShouldFollowConvention(Architecture, "Granit.");

    [Fact]
    public void IOwnable_types_should_have_private_set_on_OwnerId() =>
        DomainConventionRules.IOwnableTypesShouldHavePrivateSetOnOwnerId(Architecture, "Granit.");

    /// <summary>
    /// No type should manually implement <c>IDomainEventSource</c> — use aggregate root base classes instead.
    /// </summary>
    [Fact]
    public void No_manual_IDomainEventSource_implementors()
    {
        IReadOnlyList<string> violators =
            DomainConventionRules.FindManualDomainEventSourceImplementors(
                Architecture, "Granit.");

        violators.ShouldBeEmpty(
            "Types should inherit from AggregateRoot (or audited variants) instead of manually " +
            "implementing IDomainEventSource. " +
            $"Violators: {string.Join(", ", violators)}");
    }
}
