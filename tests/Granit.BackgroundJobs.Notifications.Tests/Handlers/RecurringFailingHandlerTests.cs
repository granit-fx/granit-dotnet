using Granit.BackgroundJobs.Events;
using Granit.BackgroundJobs.Notifications.Handlers;
using Granit.Notifications.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.BackgroundJobs.Notifications.Tests.Handlers;

public sealed class RecurringFailingHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesRecurringFailingNotification_ToSubscribers()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var jobId = Guid.NewGuid();
        const string jobName = "blob-storage-orphan-cleanup";
        const int failureCount = 3;
        const string lastError = "Connection refused: blob endpoint unreachable.";
        BackgroundJobFailureThresholdExceededEto evt = new(jobId, jobName, failureCount, lastError);

        await RecurringFailingHandler.HandleAsync(evt, publisher, CancellationToken.None);

        await publisher.Received(1).PublishToSubscribersAsync(
            JobsRecurringFailingNotificationType.Instance,
            Arg.Is<JobsRecurringFailingNotificationData>(d =>
                d.JobId == jobId
                && d.JobName == jobName
                && d.ConsecutiveFailureCount == failureCount
                && d.LastErrorMessage == lastError),
            Arg.Any<CancellationToken>());
    }
}
