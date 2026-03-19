using System.Reflection;
using Granit.Core.Domain;
using Granit.Core.Events;
using Granit.Workflow.Domain;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

/// <summary>
/// Tests for <see cref="VersionedWorkflowEntity"/> base class.
/// </summary>
public sealed class VersionedWorkflowEntityTests
{
    // ========================================================================
    // Property defaults
    // ========================================================================

    [Fact]
    public void NewEntity_ShouldHaveDefaultPropertyValues()
    {
        // Arrange & Act
        TestVersionedWorkflowEntity entity = new();

        // Assert
        entity.VersionId.ShouldBe(Guid.Empty);
        entity.Version.ShouldBe(0);
        entity.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Draft);
        entity.IsPublished.ShouldBeFalse();
    }

    // ========================================================================
    // Property setters via interface casts (same path as interceptors)
    // ========================================================================

    [Fact]
    public void Properties_ShouldBeSettableViaInterfaceCast()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        TestVersionedWorkflowEntity entity = new();

        // Act — write through interface casts (same as VersioningInterceptor / WorkflowTransitionInterceptor)
        IVersioned versioned = entity;
        versioned.VersionId = businessId;
        versioned.Version = 3;

        IVersionedEntity versionedEntity = entity;
        versionedEntity.LifecycleStatus = WorkflowLifecycleStatus.Published;

        IPublishable publishable = entity;
        publishable.IsPublished = true;

        // Assert — read through concrete type
        entity.VersionId.ShouldBe(businessId);
        entity.Version.ShouldBe(3);
        entity.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Published);
        entity.IsPublished.ShouldBeTrue();
    }

    // ========================================================================
    // GetWorkflowEntityId
    // ========================================================================

    [Fact]
    public void GetWorkflowEntityId_ShouldReturnIdAsString()
    {
        // Arrange
        var id = Guid.NewGuid();
        TestVersionedWorkflowEntity entity = new() { Id = id };

        // Act
        string entityId = entity.GetWorkflowEntityId();

        // Assert
        entityId.ShouldBe(id.ToString());
    }

    // ========================================================================
    // IWorkflowStateful.StatusPropertyName (resolved via reflection)
    // ========================================================================

    [Fact]
    public void StatusPropertyName_ShouldReturnLifecycleStatus()
    {
        // Arrange & Act — access via static property (same path as interceptor)
        string propertyName = GetStaticProperty<string>(
            typeof(TestVersionedWorkflowEntity), "StatusPropertyName");

        // Assert
        propertyName.ShouldBe("LifecycleStatus");
    }

    // ========================================================================
    // IWorkflowStateful.WorkflowEntityType — derived class override
    // ========================================================================

    [Fact]
    public void WorkflowEntityType_DerivedOverride_ShouldReturnCustomValue()
    {
        // Arrange & Act — resolved via reflection as the interceptor does
        string entityType = GetStaticProperty<string>(
            typeof(TestVersionedWorkflowEntity), "WorkflowEntityType");

        // Assert
        entityType.ShouldBe("TestDocument");
    }

    // ========================================================================
    // AuditedAggregateRoot inheritance
    // ========================================================================

    [Fact]
    public void Entity_ShouldInheritAuditedAggregateRootProperties()
    {
        // Arrange
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TestVersionedWorkflowEntity entity = new()
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            CreatedBy = "user-1",
            ModifiedAt = now,
            ModifiedBy = "user-2",
        };

        // Assert
        entity.CreatedAt.ShouldBe(now);
        entity.CreatedBy.ShouldBe("user-1");
        entity.ModifiedAt.ShouldBe(now);
        entity.ModifiedBy.ShouldBe("user-2");
    }

    // ========================================================================
    // IVersionedEntity implementation
    // ========================================================================

    [Fact]
    public void Entity_ShouldImplementIVersionedEntity()
    {
        // Arrange & Act — set via interface casts
        var businessId = Guid.NewGuid();
        TestVersionedWorkflowEntity entity = new();

        IVersioned versioned = entity;
        versioned.VersionId = businessId;
        versioned.Version = 5;

        IVersionedEntity versionedEntity = entity;
        versionedEntity.LifecycleStatus = WorkflowLifecycleStatus.Archived;

        IPublishable publishable = entity;
        publishable.IsPublished = false;

        // Assert — cast to interface
        versionedEntity.VersionId.ShouldBe(businessId);
        versionedEntity.Version.ShouldBe(5);
        versionedEntity.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Archived);
        versionedEntity.IsPublished.ShouldBeFalse();
    }

    [Fact]
    public void Entity_ShouldImplementIWorkflowStateful()
    {
        // Arrange
        var id = Guid.NewGuid();
        TestVersionedWorkflowEntity entity = new() { Id = id };

        // Act — cast to interface
        IWorkflowStateful stateful = entity;

        // Assert
        stateful.GetWorkflowEntityId().ShouldBe(id.ToString());
    }

    // ========================================================================
    // Domain & integration event support (from AuditedAggregateRoot)
    // ========================================================================

    [Fact]
    public void Entity_ShouldSupportDomainEvents()
    {
        // Arrange
        TestVersionedWorkflowEntity entity = new();

        // Act
        entity.RaiseDomainEvent(new TestDomainEvent("test-payload"));

        // Assert
        entity.DomainEvents.ShouldHaveSingleItem();
        entity.DomainEvents.First().ShouldBeOfType<TestDomainEvent>()
            .Payload.ShouldBe("test-payload");
    }

    [Fact]
    public void Entity_ShouldSupportIntegrationEvents()
    {
        // Arrange
        TestVersionedWorkflowEntity entity = new();

        // Act
        entity.RaiseIntegrationEvent(new TestIntegrationEto("test-data"));

        // Assert
        entity.IntegrationEvents.ShouldHaveSingleItem();
        entity.IntegrationEvents.First().ShouldBeOfType<TestIntegrationEto>()
            .Data.ShouldBe("test-data");
    }

    // ========================================================================
    // SetLifecycleStatus behavior method
    // ========================================================================

    [Fact]
    public void SetLifecycleStatus_ShouldUpdateStatus()
    {
        // Arrange
        TestVersionedWorkflowEntity entity = new();
        entity.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Draft);

        // Act
        entity.TransitionTo(WorkflowLifecycleStatus.PendingReview);

        // Assert
        entity.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.PendingReview);
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static T GetStaticProperty<T>(Type type, string propertyName) =>
        (T)type.GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!
            .GetValue(null)!;

    // ========================================================================
    // Test types
    // ========================================================================

    private sealed class TestVersionedWorkflowEntity : VersionedWorkflowEntity, IWorkflowStateful
    {
        public static string StatusPropertyName => nameof(LifecycleStatus);
        public static string WorkflowEntityType => "TestDocument";

        /// <summary>Exposes <see cref="AddDomainEvent"/> for testing.</summary>
        public void RaiseDomainEvent(IDomainEvent domainEvent) => AddDomainEvent(domainEvent);

        /// <summary>Exposes <see cref="AddDistributedEvent"/> for testing.</summary>
        public void RaiseIntegrationEvent(IIntegrationEvent integrationEvent) => AddDistributedEvent(integrationEvent);

        /// <summary>Exposes <see cref="SetLifecycleStatus"/> for testing.</summary>
        public void TransitionTo(WorkflowLifecycleStatus status) => SetLifecycleStatus(status);
    }

    private sealed record TestDomainEvent(string Payload) : IDomainEvent;

    private sealed record TestIntegrationEto(string Data) : IIntegrationEvent;
}
