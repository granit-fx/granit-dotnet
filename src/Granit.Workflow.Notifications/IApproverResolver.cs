namespace Granit.Workflow.Notifications;

/// <summary>
/// Resolves the user IDs of users who are authorized to approve a workflow transition
/// that requires a specific permission.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation resolves approvers from Keycloak roles via
/// <c>Granit.Users</c>. Consumers can override with custom logic
/// (e.g., department-based, hierarchy-based, or delegation-based approvers).
/// </para>
/// <para>
/// Called by the <see cref="Handlers.WorkflowApprovalRequestedHandler"/> when a
/// transition is routed to approval because the requesting user lacks the
/// required permission.
/// </para>
/// </remarks>
public interface IApproverResolver
{
    /// <summary>
    /// Returns user IDs of users who hold the required permission and can approve
    /// the transition.
    /// </summary>
    /// <param name="requiredPermission">The permission required for the transition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of approver user IDs. Empty if no approvers found.</returns>
    Task<IReadOnlyList<string>> ResolveApproversAsync(
        string requiredPermission,
        CancellationToken cancellationToken = default);
}
