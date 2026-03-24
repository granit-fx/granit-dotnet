using Granit.MultiTenancy;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Workflow.Events;
using Granit.Workflow.Notifications.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Notifications.Tests;

/// <summary>
/// Additional tests for <see cref="WorkflowApprovalRequestedHandler"/> covering tenant context.
/// </summary>
public sealed class WorkflowApprovalRequestedHandlerAdditionalTests
{
    private readonly IApproverResolver _approverResolver = Substitute.For<IApproverResolver>();
    private readonly INotificationPublisher _publisher = Substitute.For<INotificationPublisher>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly WorkflowApprovalRequestedHandler _handler;

    private static readonly WorkflowApprovalRequestedEvent SampleEvent = new(
        EntityType: "Document",
        EntityId: "doc-1",
        RequestedBy: "user-5",
        TargetState: "Published",
        RequiredPermission: "workflow.publish");

    public WorkflowApprovalRequestedHandlerAdditionalTests()
    {
        _handler = new WorkflowApprovalRequestedHandler(
            _approverResolver,
            _publisher,
            _currentTenant,
            NullLogger<WorkflowApprovalRequestedHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WithTenantContext_ShouldPublishNotification()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        _approverResolver.ResolveApproversAsync("workflow.publish", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)["approver-1"]);

        // Act
        await _handler.HandleAsync(SampleEvent, TestContext.Current.CancellationToken);

        // Assert
        await _publisher.Received(1).PublishAsync(
            Arg.Any<NotificationType<WorkflowApprovalNotificationData>>(),
            Arg.Any<WorkflowApprovalNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<EntityReference>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_UsesCorrectApproverResolverPermission()
    {
        // Arrange
        _currentTenant.IsAvailable.Returns(false);
        _approverResolver.ResolveApproversAsync("workflow.publish", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)[]);

        // Act
        await _handler.HandleAsync(SampleEvent, TestContext.Current.CancellationToken);

        // Assert
        await _approverResolver.Received(1).ResolveApproversAsync(
            "workflow.publish", Arg.Any<CancellationToken>());
    }
}
