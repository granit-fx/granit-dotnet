namespace Granit.Workflow.Domain;

/// <summary>
/// Marker interface for entities whose workflow state transitions should be
/// automatically recorded in the <see cref="WorkflowTransitionRecord"/> audit trail
/// by the <c>WorkflowTransitionInterceptor</c>.
/// </summary>
/// <remarks>
/// <para>
/// The interceptor compares <c>OriginalValues</c> vs <c>CurrentValues</c> for the
/// property identified by <see cref="StatusPropertyName"/>. When a difference is
/// detected on <c>SaveChanges</c>, a new <see cref="WorkflowTransitionRecord"/>
/// is added to the same transaction.
/// </para>
/// <para>
/// Uses C# 11+ static abstract members to avoid instance-level overhead.
/// The interceptor resolves these via the concrete type at runtime.
/// </para>
/// </remarks>
public interface IWorkflowStateful
{
    /// <summary>
    /// The CLR property name that holds the workflow state (e.g. <c>"Status"</c>).
    /// Must be mapped to a column in EF Core.
    /// </summary>
    static abstract string StatusPropertyName { get; }

    /// <summary>
    /// Logical entity type name for audit trail identification (e.g. <c>"Document"</c>).
    /// Used in <see cref="WorkflowTransitionRecord.EntityType"/>.
    /// </summary>
    static abstract string WorkflowEntityType { get; }

    /// <summary>
    /// Returns the entity identifier as a string for polymorphic audit trail storage.
    /// Typically returns <c>Id.ToString()</c>.
    /// </summary>
    string GetWorkflowEntityId();
}
