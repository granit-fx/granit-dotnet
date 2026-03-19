namespace Granit.Workflow;

/// <summary>
/// Represents a single allowed state transition in a workflow definition.
/// Immutable record capturing the source state, target state, permission requirements,
/// and approval routing configuration.
/// </summary>
/// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
public sealed record WorkflowTransition<TState> where TState : struct, Enum
{
    /// <summary>Source state of the transition.</summary>
    public required TState From { get; init; }

    /// <summary>Target state of the transition.</summary>
    public required TState To { get; init; }

    /// <summary>
    /// Display name for the transition (e.g. "Publier", "Archiver").
    /// Used by the frontend <c>WorkflowStatusBar</c> to label action buttons.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Permission string required to execute this transition (e.g. "document.publish").
    /// Checked via <c>IPermissionChecker</c> from Granit.Security.
    /// <c>null</c> means no permission check — any authenticated user can trigger the transition.
    /// </summary>
    public string? RequiredPermission { get; init; }

    /// <summary>
    /// When <c>true</c> and the current user lacks <see cref="RequiredPermission"/>,
    /// the transition is routed to a pending review state instead of being denied.
    /// A <see cref="Events.WorkflowApprovalRequestedEvent"/> domain event is published
    /// to notify designated approvers.
    /// </summary>
    public bool RequiresApproval { get; init; }
}
