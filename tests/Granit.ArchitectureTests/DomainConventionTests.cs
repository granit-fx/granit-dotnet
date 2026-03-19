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

    /// <summary>
    /// Types that manually implement <c>IDomainEventSource</c> instead of inheriting from
    /// an aggregate root base class. These are migration candidates.
    /// </summary>
    /// <remarks>
    /// Allowlist will shrink as entities are migrated to aggregate roots (Phases 1-3).
    /// </remarks>
    [Fact]
    public void Manual_IDomainEventSource_implementors_should_use_aggregate_root_bases()
    {
        // Allowlist: entities not yet migrated to aggregate root bases.
        // Remove entries as they are migrated (Phases 1-3 of the DDD improvement plan).
        string[] allowlist =
        [
            "Granit.Timeline.Domain.TimelineEntry",
        ];

        IReadOnlyList<string> violators =
            DomainConventionRules.FindManualDomainEventSourceImplementors(
                Architecture, "Granit.", allowlist);

        violators.ShouldBeEmpty(
            "Types should inherit from AggregateRoot (or audited variants) instead of manually " +
            "implementing IDomainEventSource. " +
            $"Violators: {string.Join(", ", violators)}");
    }
}
