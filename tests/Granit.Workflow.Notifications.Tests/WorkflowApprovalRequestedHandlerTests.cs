using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Workflow.Events;
using Granit.Workflow.Notifications.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.Workflow.Notifications.Tests;

public sealed class WorkflowApprovalRequestedHandlerTests
{
    private readonly IApproverResolver _approverResolver = Substitute.For<IApproverResolver>();
    private readonly INotificationPublisher _publisher = Substitute.For<INotificationPublisher>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly WorkflowApprovalRequestedHandler _handler;

    private static readonly WorkflowApprovalRequestedEvent SampleEvent = new(
        EntityType: "Patient",
        EntityId: "42",
        RequestedBy: "user-1",
        TargetState: "Published",
        RequiredPermission: "workflow.publish");

    public WorkflowApprovalRequestedHandlerTests()
    {
        _currentTenant.IsAvailable.Returns(false);

        _handler = new WorkflowApprovalRequestedHandler(
            _approverResolver,
            _publisher,
            _currentTenant,
            NullLogger<WorkflowApprovalRequestedHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WithApprovers_PublishesNotification()
    {
        _approverResolver.ResolveApproversAsync("workflow.publish", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)["approver-1", "approver-2"]);

        await _handler.HandleAsync(SampleEvent, TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishAsync(
            WorkflowApprovalNotificationType.Instance,
            Arg.Is<WorkflowApprovalNotificationData>(d =>
                d.EntityType == "Patient" &&
                d.EntityId == "42" &&
                d.RequestedBy == "user-1" &&
                d.TargetState == "Published" &&
                d.RequiredPermission == "workflow.publish"),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 2),
            Arg.Is<EntityReference>(e => e.EntityType == "Patient" && e.EntityId == "42"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NoApprovers_DoesNotPublish()
    {
        _approverResolver.ResolveApproversAsync("workflow.publish", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)[]);

        await _handler.HandleAsync(SampleEvent, TestContext.Current.CancellationToken);

        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<NotificationType<WorkflowApprovalNotificationData>>(),
            Arg.Any<WorkflowApprovalNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PassesCorrectEntityReference()
    {
        _approverResolver.ResolveApproversAsync("workflow.publish", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)["approver-1"]);

        await _handler.HandleAsync(SampleEvent, TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishAsync(
            Arg.Any<NotificationType<WorkflowApprovalNotificationData>>(),
            Arg.Any<WorkflowApprovalNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Is<EntityReference>(e => e.EntityType == "Patient" && e.EntityId == "42"),
            Arg.Any<CancellationToken>());
    }
}
