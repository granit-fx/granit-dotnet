using Granit.BackgroundJobs.Events;
using Granit.Events;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests.Events;

public sealed class BackgroundJobEventsTests
{
    // =========================================================================
    // BackgroundJobPausedEvent
    // =========================================================================

    [Fact]
    public void BackgroundJobPausedEvent_ImplementsIDomainEvent()
    {
        BackgroundJobPausedEvent evt = new(Guid.NewGuid(), "test-job");

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void BackgroundJobPausedEvent_SetsProperties()
    {
        var jobId = Guid.NewGuid();
        BackgroundJobPausedEvent evt = new(jobId, "test-job");

        evt.JobId.ShouldBe(jobId);
        evt.JobName.ShouldBe("test-job");
    }

    // =========================================================================
    // BackgroundJobResumedEvent
    // =========================================================================

    [Fact]
    public void BackgroundJobResumedEvent_ImplementsIDomainEvent()
    {
        BackgroundJobResumedEvent evt = new(Guid.NewGuid(), "test-job");

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void BackgroundJobResumedEvent_SetsProperties()
    {
        var jobId = Guid.NewGuid();
        BackgroundJobResumedEvent evt = new(jobId, "test-job");

        evt.JobId.ShouldBe(jobId);
        evt.JobName.ShouldBe("test-job");
    }

    // =========================================================================
    // BackgroundJobDefinitionChangedEvent
    // =========================================================================

    [Fact]
    public void BackgroundJobDefinitionChangedEvent_ImplementsIDomainEvent()
    {
        BackgroundJobDefinitionChangedEvent evt = new(Guid.NewGuid(), "test-job", "0 * * * *", "0 8 * * *");

        evt.ShouldBeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void BackgroundJobDefinitionChangedEvent_SetsProperties()
    {
        var jobId = Guid.NewGuid();
        BackgroundJobDefinitionChangedEvent evt = new(jobId, "test-job", "0 * * * *", "0 8 * * *");

        evt.JobId.ShouldBe(jobId);
        evt.JobName.ShouldBe("test-job");
        evt.OldCronExpression.ShouldBe("0 * * * *");
        evt.NewCronExpression.ShouldBe("0 8 * * *");
    }

    // =========================================================================
    // BackgroundJobExecutionStartedEto
    // =========================================================================

    [Fact]
    public void BackgroundJobExecutionStartedEto_ImplementsIIntegrationEvent()
    {
        BackgroundJobExecutionStartedEto eto = new(Guid.NewGuid(), "test-job", DateTimeOffset.UtcNow);

        eto.ShouldBeAssignableTo<IIntegrationEvent>();
    }

    [Fact]
    public void BackgroundJobExecutionStartedEto_SetsProperties()
    {
        var jobId = Guid.NewGuid();
        DateTimeOffset startedAt = new(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);
        BackgroundJobExecutionStartedEto eto = new(jobId, "test-job", startedAt);

        eto.JobId.ShouldBe(jobId);
        eto.JobName.ShouldBe("test-job");
        eto.StartedAt.ShouldBe(startedAt);
    }

    // =========================================================================
    // BackgroundJobFailureThresholdExceededEto
    // =========================================================================

    [Fact]
    public void BackgroundJobFailureThresholdExceededEto_ImplementsIIntegrationEvent()
    {
        BackgroundJobFailureThresholdExceededEto eto = new(Guid.NewGuid(), "test-job", 3, "error");

        eto.ShouldBeAssignableTo<IIntegrationEvent>();
    }

    [Fact]
    public void BackgroundJobFailureThresholdExceededEto_SetsProperties()
    {
        var jobId = Guid.NewGuid();
        BackgroundJobFailureThresholdExceededEto eto = new(jobId, "test-job", 5, "critical error");

        eto.JobId.ShouldBe(jobId);
        eto.JobName.ShouldBe("test-job");
        eto.ConsecutiveFailureCount.ShouldBe(5);
        eto.LastErrorMessage.ShouldBe("critical error");
    }

    [Fact]
    public void BackgroundJobFailureThresholdExceededEto_NullLastErrorMessage()
    {
        BackgroundJobFailureThresholdExceededEto eto = new(Guid.NewGuid(), "test-job", 3, null);

        eto.LastErrorMessage.ShouldBeNull();
    }
}
