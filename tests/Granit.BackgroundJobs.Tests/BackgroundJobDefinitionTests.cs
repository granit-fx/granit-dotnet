// =============================================================================
// Tests - BackgroundJobDefinition domain events
// =============================================================================
// Verifies IDomainEventSource implementation: Pause and Resume emit
// the correct domain events, ClearDomainEvents resets the collection.
// =============================================================================

using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Events;
using Granit.Core.Events;
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
