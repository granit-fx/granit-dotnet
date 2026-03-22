using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates layered architecture dependency rules:
/// Core → Application → Infrastructure → Endpoints (no reverse).
/// See docs/patterns/architecture/layered-architecture.md.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = GranitArchitecture.Instance;

    [Fact]
    public void Core_types_should_not_depend_on_EntityFrameworkCore() =>
        LayerDependencyRules.TypesShouldNotDependOnEntityFrameworkCore(
            Architecture, "Granit.Core.", "Core layer");

    [Fact]
    public void Timing_types_should_not_depend_on_EntityFrameworkCore() =>
        LayerDependencyRules.TypesShouldNotDependOnEntityFrameworkCore(
            Architecture, "Granit.Timing", "Granit.Timing (core utility)");

    [Fact]
    public void Guids_types_should_not_depend_on_EntityFrameworkCore() =>
        LayerDependencyRules.TypesShouldNotDependOnEntityFrameworkCore(
            Architecture, "Granit.Guids", "Granit.Guids (core utility)");

    [Fact]
    public void Endpoint_types_should_not_depend_on_EntityFrameworkCore() =>
        LayerDependencyRules.EndpointTypesShouldNotDependOnEntityFrameworkCore(Architecture);

    [Fact]
    public void IQueryable_should_not_appear_in_non_persistence_types() =>
        LayerDependencyRules.IQueryableShouldNotEscapePersistenceLayer(Architecture);

    [Fact]
    public void Endpoint_types_should_not_inherit_from_domain_entities() =>
        LayerDependencyRules.EndpointTypesShouldNotInheritFromDomainEntities(
            Architecture, GranitArchitecture.DomainBaseClassFullNames);

    [Fact]
    public void Exceptions_should_not_reside_in_Endpoints() =>
        LayerDependencyRules.ExceptionsShouldNotResideInEndpoints(Architecture);

    [Fact]
    public void Exceptions_should_not_depend_on_AspNetCore() =>
        LayerDependencyRules.ExceptionsShouldNotDependOnAspNetCore(Architecture, "Granit.");
}
