using Granit.Scheduling.Domain;
using Granit.Scheduling.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Scheduling.Tests;

public sealed class ScheduledActionTests
{
    [Fact]
    public void Create_ShouldSetPendingStatus()
    {
        var action = ScheduledAction.Create(
            Guid.NewGuid(),
            "TestPayload, TestAssembly",
            """{"key":"value"}""",
            DateTimeOffset.UtcNow.AddHours(1));

        action.Status.ShouldBe(ScheduledActionStatus.Pending);
    }

    [Fact]
    public void Create_WithCorrelationId_ShouldStoreIt()
    {
        string correlationId = $"subscription:{Guid.NewGuid()}";

        var action = ScheduledAction.Create(
            Guid.NewGuid(),
            "TestPayload, TestAssembly",
            """{"key":"value"}""",
            DateTimeOffset.UtcNow.AddHours(1),
            correlationId);

        action.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public void Cancel_WhenPending_ShouldTransitionToCancelled()
    {
        var action = ScheduledAction.Create(
            Guid.NewGuid(),
            "TestPayload, TestAssembly",
            """{"key":"value"}""",
            DateTimeOffset.UtcNow.AddHours(1));

        action.Cancel("admin@test.com");

        action.Status.ShouldBe(ScheduledActionStatus.Cancelled);
        action.CancelledBy.ShouldBe("admin@test.com");
    }

    [Fact]
    public void Reschedule_WhenPending_ShouldUpdateExecuteAt()
    {
        var action = ScheduledAction.Create(
            Guid.NewGuid(),
            "TestPayload, TestAssembly",
            """{"key":"value"}""",
            DateTimeOffset.UtcNow.AddHours(1));

        DateTimeOffset newDate = DateTimeOffset.UtcNow.AddDays(7);
        action.Reschedule(newDate);

        action.ExecuteAt.ShouldBe(newDate);
        action.Status.ShouldBe(ScheduledActionStatus.Pending);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldThrow()
    {
        var action = ScheduledAction.Create(
            Guid.NewGuid(),
            "TestPayload, TestAssembly",
            """{"key":"value"}""",
            DateTimeOffset.UtcNow.AddHours(1));

        action.Cancel("admin@test.com");

        Should.Throw<InvalidOperationException>(() => action.Cancel("other@test.com"));
    }

    [Fact]
    public void ScheduledActionId_Create_WithEmptyGuid_ShouldThrow() =>
        Should.Throw<ArgumentException>(() => ScheduledActionId.Create(Guid.Empty));

    [Fact]
    public void ScheduledActionId_ImplicitConversion_ShouldRoundTrip()
    {
        var guid = Guid.NewGuid();
        ScheduledActionId id = guid;
        var result = (Guid)id;

        result.ShouldBe(guid);
    }
}
