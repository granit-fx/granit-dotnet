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

    // =========================================================================
    // BackgroundJobResumedEvent
    // =========================================================================

    [Fact]
    public void BackgroundJobResumedEvent_ImplementsIDomainEvent()
    {
        BackgroundJobResumedEvent evt = new(Guid.NewGuid(), "test-job");

        evt.ShouldBeAssignableTo<IDomainEvent>();
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

    // =========================================================================
    // BackgroundJobExecutionStartedEto
    // =========================================================================

    [Fact]
    public void BackgroundJobExecutionStartedEto_ImplementsIIntegrationEvent()
    {
        BackgroundJobExecutionStartedEto eto = new(Guid.NewGuid(), "test-job", DateTimeOffset.UtcNow);

        eto.ShouldBeAssignableTo<IIntegrationEvent>();
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
}
