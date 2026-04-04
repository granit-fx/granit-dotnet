using Granit.Domain;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Workflow.Events;
using Granit.Workflow.Notifications.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Notifications.Tests;

public sealed class WorkflowStateChangedHandlerTests
{
    private readonly INotificationPublisher _publisher = Substitute.For<INotificationPublisher>();
    private readonly WorkflowStateChangedHandler _handler;

    private static readonly WorkflowStateChangedEvent SampleEvent = new(
        EntityType: "Publication",
        EntityId: "42",
        PreviousState: "Draft",
        NewState: "Published",
        TransitionedBy: "user-1");

    public WorkflowStateChangedHandlerTests()
    {
        _handler = new WorkflowStateChangedHandler(
            _publisher,
            NullLogger<WorkflowStateChangedHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_PublishesToEntityFollowers()
    {
        await _handler.HandleAsync(SampleEvent, TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishToEntityFollowersAsync(
            WorkflowStateChangedNotificationType.Instance,
            Arg.Is<WorkflowStateChangedNotificationData>(d =>
                d.EntityType == "Publication" &&
                d.EntityId == "42" &&
                d.PreviousState == "Draft" &&
                d.NewState == "Published" &&
                d.TransitionedBy == "user-1"),
            Arg.Is<EntityReference>(e => e.EntityType == "Publication" && e.EntityId == "42"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectEntityReference()
    {
        await _handler.HandleAsync(SampleEvent, TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishToEntityFollowersAsync(
            Arg.Any<NotificationType<WorkflowStateChangedNotificationData>>(),
            Arg.Any<WorkflowStateChangedNotificationData>(),
            Arg.Is<EntityReference>(e => e.EntityType == "Publication" && e.EntityId == "42"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_UsesCorrectNotificationType()
    {
        await _handler.HandleAsync(SampleEvent, TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishToEntityFollowersAsync(
            Arg.Is<WorkflowStateChangedNotificationType>(t => t.Name == "workflow.state_changed"),
            Arg.Any<WorkflowStateChangedNotificationData>(),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>());
    }
}
