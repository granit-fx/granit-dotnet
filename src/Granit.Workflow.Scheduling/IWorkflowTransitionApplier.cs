namespace Granit.Workflow.Scheduling;

/// <summary>
/// Applies a scheduled workflow transition to a specific entity type. One implementation per
/// workflow entity type, registered under its <c>WorkflowEntityType</c> key and resolved by
/// <see cref="IWorkflowTransitionApplierRegistry"/> at execution time.
/// </summary>
/// <remarks>
/// <para>
/// An applier is the "caller" in the <c>IWorkflowManager</c> contract: it loads the aggregate,
/// validates that the requested transition is legal, sets the entity status and calls
/// <c>SaveChangesAsync</c>. On save, <c>WorkflowTransitionInterceptor</c> records the ISO 27001
/// <c>WorkflowTransitionRecord</c> and raises <c>WorkflowStateChangedEvent</c> in the same
/// transaction — the applier gets that audit for free.
/// </para>
/// <para>
/// <strong>System-context execution (deliberate).</strong> Scheduled transitions run with no
/// authenticated user. The transition was authorized when it was scheduled (the scheduling
/// endpoint is permission-gated), and the catch-up safety net re-dispatches with no user, so an
/// applier <strong>must not</strong> re-evaluate user permissions here — it would couple
/// execution to the scheduling user's live (possibly revoked) permissions and make execution
/// non-deterministic. Validate only the <em>FSM legality</em> of the transition against the
/// <c>IWorkflowDefinition&lt;TState&gt;</c> (permission-free), not via the permission-gated
/// <c>IWorkflowManager.TransitionAsync</c>. <see cref="WorkflowTransitionApplierBase{TEntity, TState}"/>
/// encodes this correctly; derive from it rather than re-implementing the decision.
/// </para>
/// </remarks>
public interface IWorkflowTransitionApplier
{
    /// <summary>
    /// Loads the entity, validates the transition, applies <paramref name="targetState"/> and
    /// persists the change.
    /// </summary>
    /// <param name="entityId">The identifier of the entity to transition.</param>
    /// <param name="targetState">The target workflow state name (the <c>TState</c> enum member name).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ApplyAsync(Guid entityId, string targetState, CancellationToken cancellationToken = default);
}
