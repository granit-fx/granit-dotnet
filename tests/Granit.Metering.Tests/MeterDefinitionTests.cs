using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
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
        meter.Activated.ShouldBeTrue();
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
    public void Update_ShouldChangeProperties()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "Old", "old", AggregationType.Sum);

        meter.Update("New", "new", "Updated");

        meter.Name.ShouldBe("New");
        meter.Unit.ShouldBe("new");
        meter.Description.ShouldBe("Updated");
    }

    [Fact]
    public void Deactivate_ShouldSetInactive()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "Test", "unit", AggregationType.Count);

        meter.Deactivate();

        meter.Activated.ShouldBeFalse();
    }

    [Fact]
    public void Activate_AfterDeactivate_ShouldRestore()
    {
        var meter = MeterDefinition.Create(
            Guid.NewGuid(), "Test", "unit", AggregationType.Count);
        meter.Deactivate();

        meter.Activate();

        meter.Activated.ShouldBeTrue();
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
}
