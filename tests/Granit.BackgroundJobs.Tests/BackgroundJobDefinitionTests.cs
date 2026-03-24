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

        job.RecordFailure("timeout");
        job.RecordFailure("timeout");

        job.ConsecutiveFailureCount.ShouldBe(2);
        job.IntegrationEvents.ShouldBeEmpty();
    }

    [Fact]
    public void RecordFailure_AtThreshold_ShouldEmitBackgroundJobFailureThresholdExceededEto()
    {
        BackgroundJobDefinition job = BuildJob();

        job.RecordFailure("error 1");
        job.RecordFailure("error 2");
        job.RecordFailure("error 3");

        job.ConsecutiveFailureCount.ShouldBe(3);

        IIntegrationEvent integrationEvent = job.IntegrationEvents.ShouldHaveSingleItem();
        BackgroundJobFailureThresholdExceededEto eto = integrationEvent.ShouldBeOfType<BackgroundJobFailureThresholdExceededEto>();
        eto.JobId.ShouldBe(job.Id);
        eto.JobName.ShouldBe("test-job");
        eto.ConsecutiveFailureCount.ShouldBe(3);
        eto.LastErrorMessage.ShouldBe("error 3");
    }

    [Fact]
    public void RecordFailure_AboveThreshold_ShouldEmitOnEachSubsequentFailure()
    {
        BackgroundJobDefinition job = BuildJob();

        for (int i = 0; i < 5; i++)
        {
            job.RecordFailure($"error {i + 1}");
        }

        job.IntegrationEvents.Count.ShouldBe(3); // failures 3, 4, 5
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
