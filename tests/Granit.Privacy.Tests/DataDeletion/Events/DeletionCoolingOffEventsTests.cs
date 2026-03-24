using System.Reflection;
using Granit.Events;
using Granit.Privacy.DataDeletion.Events;
using Shouldly;
using Wolverine.Persistence.Sagas;
using Xunit;

namespace Granit.Privacy.Tests.DataDeletion.Events;

public sealed class DeletionCoolingOffEventsTests
{
    // ── DeletionDeferredEto ──────────────────────────────────────────────────

    [Fact]
    public void DeletionDeferredEto_ImplementsIIntegrationEvent()
    {
        var sut = new DeletionDeferredEto(
            Guid.NewGuid(), Guid.NewGuid(), "user@example.com",
            DateTimeOffset.UtcNow, "reason", DateTimeOffset.UtcNow.AddDays(30));

        sut.ShouldBeAssignableTo<IIntegrationEvent>();
    }

    [Fact]
    public void DeletionDeferredEto_SetsAllProperties()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset deadline = now.AddDays(30);

        var sut = new DeletionDeferredEto(requestId, userId, "admin@test.com", now, "Account closure", deadline);

        sut.RequestId.ShouldBe(requestId);
        sut.UserId.ShouldBe(userId);
        sut.RequestedBy.ShouldBe("admin@test.com");
        sut.RequestedAt.ShouldBe(now);
        sut.Reason.ShouldBe("Account closure");
        sut.ScheduledDeletionAt.ShouldBe(deadline);
    }

    // ── DeletionCancelledEto ─────────────────────────────────────────────────

    [Fact]
    public void DeletionCancelledEto_ImplementsIIntegrationEvent()
    {
        var sut = new DeletionCancelledEto(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        sut.ShouldBeAssignableTo<IIntegrationEvent>();
    }

    [Fact]
    public void DeletionCancelledEto_HasSagaIdentityOnRequestId()
    {
        PropertyInfo prop = typeof(DeletionCancelledEto).GetProperty(nameof(DeletionCancelledEto.RequestId))!;
        object[] attr = prop.GetCustomAttributes(typeof(SagaIdentityAttribute), false);

        attr.ShouldNotBeEmpty();
    }

    // ── DeletionReminderDueEvent ─────────────────────────────────────────────

    [Fact]
    public void DeletionReminderDueEvent_HasSagaIdentityOnRequestId()
    {
        PropertyInfo prop = typeof(DeletionReminderDueEvent).GetProperty(nameof(DeletionReminderDueEvent.RequestId))!;
        object[] attr = prop.GetCustomAttributes(typeof(SagaIdentityAttribute), false);

        attr.ShouldNotBeEmpty();
    }

    // ── DeletionDeadlineReachedEvent ─────────────────────────────────────────

    [Fact]
    public void DeletionDeadlineReachedEvent_HasSagaIdentityOnRequestId()
    {
        PropertyInfo prop = typeof(DeletionDeadlineReachedEvent).GetProperty(nameof(DeletionDeadlineReachedEvent.RequestId))!;
        object[] attr = prop.GetCustomAttributes(typeof(SagaIdentityAttribute), false);

        attr.ShouldNotBeEmpty();
    }

    // ── DeletionReminderDueEto ───────────────────────────────────────────────

    [Fact]
    public void DeletionReminderDueEto_ImplementsIIntegrationEvent()
    {
        var sut = new DeletionReminderDueEto(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(3));

        sut.ShouldBeAssignableTo<IIntegrationEvent>();
    }

    // ── DeletionExecutedEto ──────────────────────────────────────────────────

    [Fact]
    public void DeletionExecutedEto_ImplementsIIntegrationEvent()
    {
        var sut = new DeletionExecutedEto(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        sut.ShouldBeAssignableTo<IIntegrationEvent>();
    }

    [Fact]
    public void DeletionExecutedEto_SetsAllProperties()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        DateTimeOffset executedAt = DateTimeOffset.UtcNow;

        var sut = new DeletionExecutedEto(requestId, userId, executedAt);

        sut.RequestId.ShouldBe(requestId);
        sut.UserId.ShouldBe(userId);
        sut.ExecutedAt.ShouldBe(executedAt);
    }
}
