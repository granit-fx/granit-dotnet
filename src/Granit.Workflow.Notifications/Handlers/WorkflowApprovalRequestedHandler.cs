using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Workflow.Events;
using Microsoft.Extensions.Logging;

namespace Granit.Workflow.Notifications.Handlers;

/// <summary>
/// Wolverine handler that processes <see cref="WorkflowApprovalRequestedEvent"/> domain events
/// by notifying designated approvers via <see cref="INotificationPublisher"/>.
/// </summary>
/// <remarks>
/// <para>
/// When a user without the required permission triggers a transition that supports
/// approval routing (<c>RequiresApproval = true</c>), the workflow engine publishes
/// a <see cref="WorkflowApprovalRequestedEvent"/> event. This handler:
/// </para>
/// <list type="number">
///   <item>Resolves approver user IDs via <see cref="IApproverResolver"/>.</item>
///   <item>Sends a notification to each approver via <see cref="INotificationPublisher"/>.</item>
/// </list>
/// <para>
/// If no approvers are found, the handler logs a warning and returns without error.
/// This follows the graceful degradation pattern.
/// </para>
/// </remarks>
public sealed partial class WorkflowApprovalRequestedHandler(
    IApproverResolver approverResolver,
    INotificationPublisher notificationPublisher,
    ICurrentTenant currentTenant,
    ILogger<WorkflowApprovalRequestedHandler> logger)
{
    /// <summary>
    /// Handles the <see cref="WorkflowApprovalRequestedEvent"/> event by notifying approvers.
    /// </summary>
    public async Task HandleAsync(
        WorkflowApprovalRequestedEvent message,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> approverIds = await approverResolver.ResolveApproversAsync(
            message.RequiredPermission, cancellationToken).ConfigureAwait(false);

        if (approverIds.Count == 0)
        {
            LogNoApproversFound(message.RequiredPermission, message.EntityType, message.EntityId);
            return;
        }

        WorkflowApprovalNotificationData data = new(
            message.EntityType,
            message.EntityId,
            message.RequestedBy,
            message.TargetState,
            message.RequiredPermission);

        EntityReference relatedEntity = new(message.EntityType, message.EntityId);

        await notificationPublisher.PublishAsync(
            WorkflowApprovalNotificationType.Instance,
            data,
            approverIds,
            relatedEntity,
            cancellationToken).ConfigureAwait(false);

        LogApprovalNotificationSent(approverIds.Count, message.EntityType, message.EntityId, message.RequiredPermission, currentTenant.IsAvailable ? currentTenant.Id : null);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No approvers found for permission {Permission} on {EntityType} {EntityId}. The approval request will not be delivered")]
    private partial void LogNoApproversFound(string permission, string entityType, string entityId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Approval notification sent to {ApproverCount} approvers for {EntityType} {EntityId} (permission: {Permission}, tenant: {TenantId})")]
    private partial void LogApprovalNotificationSent(int approverCount, string entityType, string entityId, string permission, Guid? tenantId);
}
