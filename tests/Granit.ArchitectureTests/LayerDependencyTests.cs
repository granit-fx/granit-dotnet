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

    /// <summary>
    /// Types in the base Granit package (Domain, Events, Users, Modularity, etc.)
    /// must not depend on EF Core. These are the foundational abstractions.
    /// </summary>
    [Theory]
    [InlineData("Granit.DataFiltering")]
    [InlineData("Granit.Diagnostics")]
    [InlineData("Granit.Domain")]
    [InlineData("Granit.Endpoints")]
    [InlineData("Granit.Events")]
    [InlineData("Granit.Exceptions")]
    [InlineData("Granit.Extensions")]
    [InlineData("Granit.Json")]
    [InlineData("Granit.Localization")]
    [InlineData("Granit.Modularity")]
    [InlineData("Granit.MultiTenancy")]
    [InlineData("Granit.Users")]
    public void Base_package_types_should_not_depend_on_EntityFrameworkCore(string namespacePrefix) =>
        LayerDependencyRules.ExactNamespaceShouldNotDependOnEntityFrameworkCore(
            Architecture, namespacePrefix, $"Base package ({namespacePrefix})");

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
