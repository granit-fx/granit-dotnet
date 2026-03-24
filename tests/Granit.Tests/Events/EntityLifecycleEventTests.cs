// =============================================================================
// Tests — entity lifecycle event records and opt-in interfaces
// =============================================================================

using Granit.Domain;
using Granit.Events;
using Shouldly;
using Xunit;

namespace Granit.Tests.Events;

public sealed class EntityLifecycleEventTests
{
    // -------------------------------------------------------------------------
    // IDomainEvent implementations
    // -------------------------------------------------------------------------

    [Fact]
    public void EntityCreatedEvent_ImplementsIDomainEvent() =>
        typeof(IDomainEvent).IsAssignableFrom(typeof(EntityCreatedEvent<TestEntity>)).ShouldBeTrue();

    [Fact]
    public void EntityUpdatedEvent_ImplementsIDomainEvent() =>
        typeof(IDomainEvent).IsAssignableFrom(typeof(EntityUpdatedEvent<TestEntity>)).ShouldBeTrue();

    [Fact]
    public void EntityDeletedEvent_ImplementsIDomainEvent() =>
        typeof(IDomainEvent).IsAssignableFrom(typeof(EntityDeletedEvent<TestEntity>)).ShouldBeTrue();

    // -------------------------------------------------------------------------
    // IIntegrationEvent implementations
    // -------------------------------------------------------------------------

    [Fact]
    public void EntityCreatedEto_ImplementsIIntegrationEvent() =>
        typeof(IIntegrationEvent).IsAssignableFrom(typeof(EntityCreatedEto<TestEto>)).ShouldBeTrue();

    [Fact]
    public void EntityUpdatedEto_ImplementsIIntegrationEvent() =>
        typeof(IIntegrationEvent).IsAssignableFrom(typeof(EntityUpdatedEto<TestEto>)).ShouldBeTrue();

    [Fact]
    public void EntityDeletedEto_ImplementsIIntegrationEvent() =>
        typeof(IIntegrationEvent).IsAssignableFrom(typeof(EntityDeletedEto<TestEto>)).ShouldBeTrue();

    // -------------------------------------------------------------------------
    // Event record properties
    // -------------------------------------------------------------------------

    [Fact]
    public void EntityCreatedEvent_CarriesEntity()
    {
        TestEntity entity = new();
        EntityCreatedEvent<TestEntity> evt = new(entity);

        evt.Entity.ShouldBeSameAs(entity);
    }

    [Fact]
    public void EntityUpdatedEvent_CarriesEntity()
    {
        TestEntity entity = new();
        EntityUpdatedEvent<TestEntity> evt = new(entity);

        evt.Entity.ShouldBeSameAs(entity);
    }

    [Fact]
    public void EntityDeletedEvent_CarriesEntity()
    {
        TestEntity entity = new();
        EntityDeletedEvent<TestEntity> evt = new(entity);

        evt.Entity.ShouldBeSameAs(entity);
    }

    [Fact]
    public void EntityCreatedEto_CarriesEto()
    {
        TestEto eto = new("created");
        EntityCreatedEto<TestEto> evt = new(eto);

        evt.Eto.ShouldBeSameAs(eto);
    }

    // -------------------------------------------------------------------------
    // Interface opt-in relationships
    // -------------------------------------------------------------------------

    [Fact]
    public void IHasEntityEto_ImpliesIEmitEntityLifecycleEvents() =>
        typeof(IHasEntityEto<TestEto>).GetInterfaces()
            .ShouldContain(typeof(IEmitEntityLifecycleEvents));

    [Fact]
    public void TestEntityWithEto_ImplementsIEmitEntityLifecycleEvents() =>
        new TestEntityWithEto().ShouldBeAssignableTo<IEmitEntityLifecycleEvents>();

    [Fact]
    public void TestEntityWithEto_ImplementsIEntityEtoProvider() =>
        new TestEntityWithEto().ShouldBeAssignableTo<IEntityEtoProvider>();

    [Fact]
    public void IHasEntityEto_GetEto_ReturnsCorrectTypeAndInstance()
    {
        TestEntityWithEto entity = new();
        IEntityEtoProvider provider = entity;

        (Type etoType, object eto) = provider.GetEto();

        etoType.ShouldBe(typeof(TestEto));
        eto.ShouldBeOfType<TestEto>();
    }

    // -------------------------------------------------------------------------
    // Test fixtures
    // -------------------------------------------------------------------------

    private sealed class TestEntity : Entity, IEmitEntityLifecycleEvents;

    private sealed record TestEto(string Name = "");

    private sealed class TestEntityWithEto : Entity, IHasEntityEto<TestEto>
    {
        public TestEto ToEto() => new("snapshot");
    }
}
