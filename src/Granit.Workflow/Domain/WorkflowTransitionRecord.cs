using Granit.Domain;

namespace Granit.Workflow.Domain;

/// <summary>
/// Immutable audit record of a workflow state transition.
/// </summary>
/// <remarks>
/// <para>
/// ISO 27001 compliance: this entity is INSERT-only. It must never be modified or deleted.
/// Do NOT implement <see cref="ISoftDeletable"/> or use <c>AuditedEntity</c> — soft-delete
/// is explicitly prohibited to preserve the 3-year audit trail.
/// </para>
/// <para>
/// Inherits from <see cref="Entity"/> (provides <c>Guid Id</c> only).
/// The record captures the complete transition context: who, when, from which state,
/// to which state, and an optional regulatory comment/justification.
/// </para>
/// </remarks>
public sealed class WorkflowTransitionRecord : Entity, IMultiTenant
{
    /// <summary>Logical entity type name (e.g. <c>"Document"</c>, <c>"Invoice"</c>).</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Identifier of the entity that transitioned (string for polymorphism).</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Workflow state before the transition.</summary>
    public string PreviousState { get; set; } = string.Empty;

    /// <summary>Workflow state after the transition.</summary>
    public string NewState { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the transition occurred (from <c>IClock.Now</c>).</summary>
    public DateTimeOffset TransitionedAt { get; set; }

    /// <summary>
    /// UserId of the user who triggered the transition, or <c>"system"</c> for
    /// automated transitions (Wolverine background handlers).
    /// </summary>
    public string TransitionedBy { get; set; } = string.Empty;

    /// <summary>
    /// Optional comment or regulatory justification for the transition.
    /// Set via <see cref="WorkflowTransitionContext.SetComment"/> (AsyncLocal).
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Tenant context at the time of transition.
    /// <c>null</c> when multi-tenancy is not active.
    /// </summary>
    public Guid? TenantId { get; set; }
}
