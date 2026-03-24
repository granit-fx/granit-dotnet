using Granit.Events;

namespace Granit.Workflow.Events;

/// <summary>
/// Domain event published when a transition is routed to approval because the
/// requesting user lacks the required permission. Handled by
/// <c>Granit.Workflow.Notifications</c> to notify designated approvers.
/// </summary>
public sealed record WorkflowApprovalRequestedEvent(
    string EntityType,
    string EntityId,
    string RequestedBy,
    string TargetState,
    string RequiredPermission) : IDomainEvent;
