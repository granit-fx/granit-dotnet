using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.Workflow.Domain;
using Shouldly;
using Xunit;

namespace Granit.Metering.Tests;

public sealed class MeterDefinitionTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "API Calls", "requests", AggregationType.Sum, "HTTP requests");

        meter.Name.ShouldBe("API Calls");
        meter.Unit.ShouldBe("requests");
        meter.AggregationType.ShouldBe(AggregationType.Sum);
        meter.Description.ShouldBe("HTTP requests");
        meter.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Draft);
#pragma warning disable CS0618
        meter.Activated.ShouldBeFalse();
#pragma warning restore CS0618
    }

    [Fact]
    public void Create_WithNullName_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            MeterDefinition.Create(Guid.NewGuid(), null!, "requests", AggregationType.Sum));
    }

    [Fact]
    public void Create_WithNullUnit_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            MeterDefinition.Create(Guid.NewGuid(), "API Calls", null!, AggregationType.Sum));
    }

    [Fact]
    public void Update_OnDraft_ShouldChangeProperties()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "Old", "old", AggregationType.Sum);

        meter.Update("New", "new", "Updated");

        meter.Name.ShouldBe("New");
        meter.Unit.ShouldBe("new");
        meter.Description.ShouldBe("Updated");
    }

    [Fact]
    public void Update_OnPublished_ShouldThrow()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "Old", "old", AggregationType.Sum);
        meter.Publish();

        Should.Throw<InvalidOperationException>(() => meter.Update("New", "new", null));
    }

    [Fact]
    public void Publish_FromDraft_ShouldTransitionToPublished()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "Test", "unit", AggregationType.Count);

        meter.Publish();

        meter.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Published);
#pragma warning disable CS0618
        meter.Activated.ShouldBeTrue();
#pragma warning restore CS0618
    }

    [Fact]
    public void Publish_FromPublished_ShouldThrow()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "Test", "unit", AggregationType.Count);
        meter.Publish();

        Should.Throw<InvalidOperationException>(() => meter.Publish());
    }

    [Fact]
    public void Archive_FromPublished_ShouldTransitionToArchived()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "Test", "unit", AggregationType.Count);
        meter.Publish();

        meter.Archive();

        meter.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Archived);
#pragma warning disable CS0618
        meter.Activated.ShouldBeFalse();
#pragma warning restore CS0618
    }

    [Fact]
    public void Archive_FromDraft_ShouldThrow()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "Test", "unit", AggregationType.Count);

        Should.Throw<InvalidOperationException>(() => meter.Archive());
    }

    [Fact]
    public void DeactivateAlias_ShouldArchivePublishedMeter()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "Test", "unit", AggregationType.Count);
        meter.Publish();

#pragma warning disable CS0618
        meter.Deactivate();
#pragma warning restore CS0618

        meter.LifecycleStatus.ShouldBe(WorkflowLifecycleStatus.Archived);
    }

    [Fact]
    public void GetWorkflowEntityId_ShouldReturnIdAsString()
    {
        var id = Guid.NewGuid();
        var meter = MeterDefinition.Create(id, "Test", "unit", AggregationType.Count);

        meter.GetWorkflowEntityId().ShouldBe(id.ToString());
    }

    [Fact]
    public void MeterDefinitionId_Create_WithEmptyGuid_ShouldThrow() =>
        Should.Throw<ArgumentException>(() => MeterDefinitionId.Create(Guid.Empty));

    [Fact]
    public void MeterDefinitionId_ImplicitConversion_ShouldRoundTrip()
    {
        var guid = Guid.NewGuid();
        MeterDefinitionId id = guid;
        Guid result = id;

        result.ShouldBe(guid);
    }

    // ======== Update guards ========

    [Fact]
    public void Update_WithNullName_ShouldThrow()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "API Calls", "requests", AggregationType.Sum);

        Should.Throw<ArgumentException>(() => meter.Update(null!, "requests", null));
    }

    [Fact]
    public void Update_WithNullUnit_ShouldThrow()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "API Calls", "requests", AggregationType.Sum);

        Should.Throw<ArgumentException>(() => meter.Update("API Calls", null!, null));
    }

    // ======== Create with null description ========

    [Fact]
    public void Create_WithNullDescription_ShouldSucceed()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "API Calls", "requests", AggregationType.Sum, description: null);

        meter.Description.ShouldBeNull();
        meter.Name.ShouldBe("API Calls");
    }

    // ======== ProductId — soft reference to Granit.Catalog.Product ========

    [Fact]
    public void Create_WithoutProductId_ShouldDefaultToNull()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "API Calls", "requests", AggregationType.Sum);

        meter.ProductId.ShouldBeNull();
    }

    [Fact]
    public void Create_WithProductId_ShouldStoreReference()
    {
        var productId = Guid.Parse("00000000-0000-0000-0000-000000000abc");

        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "API Calls", "requests", AggregationType.Sum,
            description: null, productId: productId);

        meter.ProductId.ShouldBe(productId);
    }

    [Fact]
    public void SetProduct_ShouldReplaceReference()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "API Calls", "requests", AggregationType.Sum);
        var productId = Guid.NewGuid();

        meter.SetProduct(productId);

        meter.ProductId.ShouldBe(productId);
    }

    [Fact]
    public void SetProduct_WithNull_ShouldDetach()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "API Calls", "requests", AggregationType.Sum,
            description: null, productId: Guid.NewGuid());

        meter.SetProduct(null);

        meter.ProductId.ShouldBeNull();
    }
}
