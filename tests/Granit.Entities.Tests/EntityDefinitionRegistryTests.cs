// =============================================================================
// Tests - EntityDefinitionRegistry (duplicate detection + lookup)
// =============================================================================

using Granit.Entities.Internal;
using Granit.Entities.Relations;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Entities.Tests;

public sealed class EntityDefinitionRegistryTests
{
    [Fact]
    public void All_IsSortedByName()
    {
        EntityDefinitionRegistry registry = Build([
            new FakeDescriptor("Zeta.Z", typeof(EntityZ)),
            new FakeDescriptor("Alpha.A", typeof(EntityA)),
            new FakeDescriptor("Mu.M", typeof(EntityM)),
        ]);

        registry.All.Select(d => d.Name).ShouldBe(["Alpha.A", "Mu.M", "Zeta.Z"]);
    }

    [Fact]
    public void GetByName_ReturnsMatchingDefinition()
    {
        EntityDefinitionRegistry registry = Build([
            new FakeDescriptor("Granit.X.X", typeof(EntityA)),
        ]);

        registry.GetByName("Granit.X.X").ShouldNotBeNull();
        registry.GetByName("Granit.Y.Y").ShouldBeNull();
    }

    [Fact]
    public void GetByEntityType_ReturnsMatchingDefinition()
    {
        EntityDefinitionRegistry registry = Build([
            new FakeDescriptor("Granit.X.X", typeof(EntityA)),
        ]);

        registry.GetByEntityType(typeof(EntityA)).ShouldNotBeNull();
        registry.GetByEntityType(typeof(EntityM)).ShouldBeNull();
    }

    [Fact]
    public void Constructor_RejectsDuplicateName()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            Build([
                new FakeDescriptor("Granit.X.Same", typeof(EntityA)),
                new FakeDescriptor("Granit.X.Same", typeof(EntityM)),
            ]));

        ex.Message.ShouldContain("Duplicate EntityDefinition name 'Granit.X.Same'");
    }

    [Fact]
    public void Constructor_RejectsDuplicateEntityType()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            Build([
                new FakeDescriptor("Granit.X.A", typeof(EntityA)),
                new FakeDescriptor("Granit.X.B", typeof(EntityA)),
            ]));

        ex.Message.ShouldContain($"Duplicate EntityDefinition for CLR type '{typeof(EntityA).FullName}'");
    }

    private static EntityDefinitionRegistry Build(IEnumerable<IEntityDefinitionDescriptor> definitions) =>
        new(definitions, [], NullLogger<EntityDefinitionRegistry>.Instance);

    private sealed record FakeDescriptor(string Name, Type EntityType) : IEntityDefinitionDescriptor
    {
        public EntityDefinitionDescriptor Descriptor => new()
        {
            Name = Name,
            EntityType = EntityType,
            MetricDefinitionTypes = [],
            DashboardDefinitionTypes = [],
            Forms = [],
            Details = [],
        };
    }

    private sealed class EntityA;
    private sealed class EntityM;
    private sealed class EntityZ;
}
