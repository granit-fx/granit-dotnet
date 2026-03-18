using Granit.Core.Domain;
using Granit.Core.Events;
using Shouldly;
using Xunit;

namespace Granit.Core.Tests.Domain;

public sealed class AggregateRootTests
{
    // -------------------------------------------------------------------------
    // AggregateRoot (base — no audit)
    // -------------------------------------------------------------------------

    [Fact]
    public void NewAggregateRoot_HasNoDomainEvents()
    {
        TestAggregate aggregate = new();

        aggregate.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void AddDomainEvent_CollectsEvents()
    {
        TestAggregate aggregate = new();

        aggregate.DoSomething();

        aggregate.DomainEvents.Count.ShouldBe(1);
        aggregate.DomainEvents.ShouldContain(e => e is SomethingHappened);
    }

    [Fact]
    public void AddDomainEvent_CollectsMultipleEvents()
    {
        TestAggregate aggregate = new();

        aggregate.DoSomething();
        aggregate.DoSomething();

        aggregate.DomainEvents.Count.ShouldBe(2);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        TestAggregate aggregate = new();
        aggregate.DoSomething();
        aggregate.DoSomething();

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void AddDomainEvent_NullEvent_Throws() =>
        Should.Throw<ArgumentNullException>(() => new TestAggregate().RaiseNull());

    [Fact]
    public void AggregateRoot_ImplementsIDomainEventSource() =>
        new TestAggregate().ShouldBeAssignableTo<IDomainEventSource>();

    [Fact]
    public void AggregateRoot_ImplementsIIntegrationEventSource() =>
        new TestAggregate().ShouldBeAssignableTo<IIntegrationEventSource>();

    [Fact]
    public void AggregateRoot_InheritsEntity() =>
        new TestAggregate().ShouldBeAssignableTo<Entity>();

    [Fact]
    public void NewAggregateRoot_HasNoIntegrationEvents()
    {
        TestAggregate aggregate = new();

        aggregate.IntegrationEvents.ShouldBeEmpty();
    }

    [Fact]
    public void AddDistributedEvent_CollectsIntegrationEvents()
    {
        TestAggregate aggregate = new();

        aggregate.Broadcast();

        aggregate.IntegrationEvents.Count.ShouldBe(1);
        aggregate.IntegrationEvents.ShouldContain(e => e is SomethingBroadcast);
    }

    [Fact]
    public void AddDistributedEvent_CollectsMultipleIntegrationEvents()
    {
        TestAggregate aggregate = new();

        aggregate.Broadcast();
        aggregate.Broadcast();

        aggregate.IntegrationEvents.Count.ShouldBe(2);
    }

    [Fact]
    public void ClearIntegrationEvents_RemovesAllEvents()
    {
        TestAggregate aggregate = new();
        aggregate.Broadcast();
        aggregate.Broadcast();

        aggregate.ClearIntegrationEvents();

        aggregate.IntegrationEvents.ShouldBeEmpty();
    }

    [Fact]
    public void AddDistributedEvent_NullEvent_Throws() =>
        Should.Throw<ArgumentNullException>(() => new TestAggregate().BroadcastNull());

    // -------------------------------------------------------------------------
    // CreationAuditedAggregateRoot
    // -------------------------------------------------------------------------

    [Fact]
    public void CreationAuditedAggregateRoot_HasAuditFields()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TestCreationAuditedAggregate aggregate = new()
        {
            CreatedAt = now,
            CreatedBy = "user-1",
        };

        aggregate.CreatedAt.ShouldBe(now);
        aggregate.CreatedBy.ShouldBe("user-1");
    }

    [Fact]
    public void CreationAuditedAggregateRoot_ImplementsIDomainEventSource() =>
        new TestCreationAuditedAggregate().ShouldBeAssignableTo<IDomainEventSource>();

    [Fact]
    public void CreationAuditedAggregateRoot_InheritsCreationAuditedEntity() =>
        new TestCreationAuditedAggregate().ShouldBeAssignableTo<CreationAuditedEntity>();

    [Fact]
    public void CreationAuditedAggregateRoot_CollectsAndClearsDomainEvents()
    {
        TestCreationAuditedAggregate aggregate = new();

        aggregate.DoSomething();
        aggregate.DomainEvents.Count.ShouldBe(1);

        aggregate.ClearDomainEvents();
        aggregate.DomainEvents.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // AuditedAggregateRoot
    // -------------------------------------------------------------------------

    [Fact]
    public void AuditedAggregateRoot_HasModificationFields()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TestAuditedAggregate aggregate = new()
        {
            ModifiedAt = now,
            ModifiedBy = "user-2",
        };

        aggregate.ModifiedAt.ShouldBe(now);
        aggregate.ModifiedBy.ShouldBe("user-2");
    }

    [Fact]
    public void AuditedAggregateRoot_InheritsCreationAuditedAggregateRoot() =>
        new TestAuditedAggregate().ShouldBeAssignableTo<CreationAuditedAggregateRoot>();

    // -------------------------------------------------------------------------
    // FullAuditedAggregateRoot
    // -------------------------------------------------------------------------

    [Fact]
    public void FullAuditedAggregateRoot_ImplementsISoftDeletable() =>
        new TestFullAuditedAggregate().ShouldBeAssignableTo<ISoftDeletable>();

    [Fact]
    public void FullAuditedAggregateRoot_HasSoftDeleteFields()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TestFullAuditedAggregate aggregate = new()
        {
            IsDeleted = true,
            DeletedAt = now,
            DeletedBy = "user-3",
        };

        aggregate.IsDeleted.ShouldBeTrue();
        aggregate.DeletedAt.ShouldBe(now);
        aggregate.DeletedBy.ShouldBe("user-3");
    }

    [Fact]
    public void FullAuditedAggregateRoot_InheritsAuditedAggregateRoot() =>
        new TestFullAuditedAggregate().ShouldBeAssignableTo<AuditedAggregateRoot>();

    [Fact]
    public void FullAuditedAggregateRoot_CollectsAndClearsDomainEvents()
    {
        TestFullAuditedAggregate aggregate = new();

        aggregate.DoSomething();
        aggregate.DomainEvents.Count.ShouldBe(1);

        aggregate.ClearDomainEvents();
        aggregate.DomainEvents.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Test fixtures
    // -------------------------------------------------------------------------

    private sealed record SomethingHappened(Guid EntityId) : IDomainEvent;

    private sealed record SomethingBroadcast(Guid EntityId) : IIntegrationEvent;

    private sealed class TestAggregate : AggregateRoot
    {
        public void DoSomething() => AddDomainEvent(new SomethingHappened(Id));
        public void RaiseNull() => AddDomainEvent(null!);
        public void Broadcast() => AddDistributedEvent(new SomethingBroadcast(Id));
        public void BroadcastNull() => AddDistributedEvent(null!);
    }

    private sealed class TestCreationAuditedAggregate : CreationAuditedAggregateRoot
    {
        public void DoSomething() => AddDomainEvent(new SomethingHappened(Id));
    }

    private sealed class TestAuditedAggregate : AuditedAggregateRoot
    {
        public void DoSomething() => AddDomainEvent(new SomethingHappened(Id));
    }

    private sealed class TestFullAuditedAggregate : FullAuditedAggregateRoot
    {
        public void DoSomething() => AddDomainEvent(new SomethingHappened(Id));
    }
}
