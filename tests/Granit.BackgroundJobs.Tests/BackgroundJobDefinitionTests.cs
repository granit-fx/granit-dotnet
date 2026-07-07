// =============================================================================
// Tests - BackgroundJobDefinition domain events
// =============================================================================
// Verifies IDomainEventSource implementation: Pause and Resume emit
// the correct domain events, ClearDomainEvents resets the collection.
// =============================================================================

using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Events;
using Granit.Events;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests;

public sealed class BackgroundJobDefinitionTests
{
    [Fact]
    public void Pause_ShouldEmitBackgroundJobPausedEventEvent()
    {
        BackgroundJobDefinition job = BuildJob();

        job.Pause();

        job.IsEnabled.ShouldBeFalse();

        IDomainEvent domainEvent = job.DomainEvents.ShouldHaveSingleItem();
        BackgroundJobPausedEvent paused = domainEvent.ShouldBeOfType<BackgroundJobPausedEvent>();
        paused.JobId.ShouldBe(job.Id);
        paused.JobName.ShouldBe("test-job");
    }

    [Fact]
    public void Resume_ShouldEmitBackgroundJobResumedEventEvent()
    {
        BackgroundJobDefinition job = BuildJob(enabled: false);

        job.Resume();

        job.IsEnabled.ShouldBeTrue();

        IDomainEvent domainEvent = job.DomainEvents.ShouldHaveSingleItem();
        BackgroundJobResumedEvent resumed = domainEvent.ShouldBeOfType<BackgroundJobResumedEvent>();
        resumed.JobId.ShouldBe(job.Id);
        resumed.JobName.ShouldBe("test-job");
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllCollectedEvents()
    {
        BackgroundJobDefinition job = BuildJob();
        job.Pause();

        job.ClearDomainEvents();

        job.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void MultipleTransitions_ShouldAccumulateEvents()
    {
        BackgroundJobDefinition job = BuildJob();

        job.Pause();
        job.Resume();

        job.DomainEvents.Count.ShouldBe(2);
        job.DomainEvents.First().ShouldBeOfType<BackgroundJobPausedEvent>();
        job.DomainEvents.Last().ShouldBeOfType<BackgroundJobResumedEvent>();
    }

    [Fact]
    public void UpdateDefinition_DifferentCron_ShouldEmitBackgroundJobDefinitionChangedEvent()
    {
        BackgroundJobDefinition job = BuildJob();
        job.ClearDomainEvents();

        job.UpdateDefinition("0 9 * * *", "TestMessage, TestAssembly");

        IDomainEvent domainEvent = job.DomainEvents.ShouldHaveSingleItem();
        BackgroundJobDefinitionChangedEvent changed = domainEvent.ShouldBeOfType<BackgroundJobDefinitionChangedEvent>();
        changed.JobId.ShouldBe(job.Id);
        changed.JobName.ShouldBe("test-job");
        changed.OldCronExpression.ShouldBe("0 8 * * *");
        changed.NewCronExpression.ShouldBe("0 9 * * *");
    }

    [Fact]
    public void UpdateDefinition_SameCron_ShouldNotEmitEvent()
    {
        BackgroundJobDefinition job = BuildJob();
        job.ClearDomainEvents();

        job.UpdateDefinition("0 8 * * *", "NewMessage, NewAssembly");

        job.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void RecordExecutionStart_ShouldEmitBackgroundJobExecutionStartedEto()
    {
        BackgroundJobDefinition job = BuildJob();
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;

        job.RecordExecutionStart(startedAt);

        IIntegrationEvent integrationEvent = job.IntegrationEvents.ShouldHaveSingleItem();
        BackgroundJobExecutionStartedEto eto = integrationEvent.ShouldBeOfType<BackgroundJobExecutionStartedEto>();
        eto.JobId.ShouldBe(job.Id);
        eto.JobName.ShouldBe("test-job");
        eto.StartedAt.ShouldBe(startedAt);
    }

    [Fact]
    public void RecordFailure_BelowThreshold_ShouldNotEmitIntegrationEvent()
    {
        BackgroundJobDefinition job = BuildJob();

        job.RecordFailure("timeout", 3);
        job.RecordFailure("timeout", 3);

        job.ConsecutiveFailureCount.ShouldBe(2);
        job.IntegrationEvents.ShouldBeEmpty();
    }

    [Fact]
    public void RecordFailure_AtThreshold_ShouldEmitBackgroundJobFailureThresholdExceededEto()
    {
        BackgroundJobDefinition job = BuildJob();

        job.RecordFailure("error 1", 3);
        job.RecordFailure("error 2", 3);
        job.RecordFailure("error 3", 3);

        job.ConsecutiveFailureCount.ShouldBe(3);

        IIntegrationEvent integrationEvent = job.IntegrationEvents.ShouldHaveSingleItem();
        BackgroundJobFailureThresholdExceededEto eto = integrationEvent.ShouldBeOfType<BackgroundJobFailureThresholdExceededEto>();
        eto.JobId.ShouldBe(job.Id);
        eto.JobName.ShouldBe("test-job");
        eto.ConsecutiveFailureCount.ShouldBe(3);
        eto.LastErrorMessage.ShouldBe("error 3");
    }

    [Fact]
    public void RecordFailure_AboveThreshold_ShouldAlertOnlyOnce()
    {
        BackgroundJobDefinition job = BuildJob();

        for (int i = 0; i < 5; i++)
        {
            job.RecordFailure($"error {i + 1}", 3);
        }

        // Alert fires exactly when the threshold is reached (failure 3), never again
        // until a successful execution resets the counter.
        job.IntegrationEvents.ShouldHaveSingleItem();
    }

    [Fact]
    public void RecordFailure_AfterSuccessReset_ShouldReAlertAtThreshold()
    {
        BackgroundJobDefinition job = BuildJob();

        for (int i = 0; i < 3; i++)
        {
            job.RecordFailure("first wave", 3);
        }

        job.RecordExecutionStart(DateTimeOffset.UtcNow);
        job.ClearIntegrationEvents();

        for (int i = 0; i < 3; i++)
        {
            job.RecordFailure("second wave", 3);
        }

        job.IntegrationEvents.ShouldHaveSingleItem()
            .ShouldBeOfType<BackgroundJobFailureThresholdExceededEto>();
    }

    [Fact]
    public void RecordFailure_LongErrorMessage_ShouldTruncate()
    {
        BackgroundJobDefinition job = BuildJob();
        string longMessage = new('x', 1000);

        job.RecordFailure(longMessage, 3);

        job.LastErrorMessage.ShouldNotBeNull();
        job.LastErrorMessage.Length.ShouldBeLessThanOrEqualTo(
            BackgroundJobDefinition.MaxErrorMessageLength + "… [truncated]".Length);
        job.LastErrorMessage.ShouldEndWith("… [truncated]");
    }

    [Fact]
    public void RecordFailure_ShortErrorMessage_ShouldNotTruncate()
    {
        BackgroundJobDefinition job = BuildJob();
        const string shortMessage = "timeout";

        job.RecordFailure(shortMessage, 3);

        job.LastErrorMessage.ShouldBe("timeout");
    }

    [Fact]
    public void RecordFailure_NullErrorMessage_ShouldStoreNull()
    {
        BackgroundJobDefinition job = BuildJob();

        job.RecordFailure(null, 3);

        job.LastErrorMessage.ShouldBeNull();
    }

    private static BackgroundJobDefinition BuildJob(bool enabled = true)
    {
        var job = BackgroundJobDefinition.Create(
            Guid.NewGuid(), "test-job", "0 8 * * *", "TestMessage, TestAssembly");

        if (!enabled)
        {
            job.Pause();
            job.ClearDomainEvents();
        }

        return job;
    }
}
