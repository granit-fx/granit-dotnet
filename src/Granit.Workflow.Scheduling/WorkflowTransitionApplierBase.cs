namespace Granit.Workflow.Scheduling;

/// <summary>
/// Reference base for an <see cref="IWorkflowTransitionApplier"/>. Encodes the
/// load → validate → set → save flow once, so a consumer only supplies the four
/// entity-specific steps. Centralises the deliberate system-context validation decision
/// (see <see cref="IWorkflowTransitionApplier"/>): the transition is validated for FSM
/// legality against the <see cref="IWorkflowDefinition{TState}"/> only — user permissions are
/// <strong>not</strong> re-evaluated at execution time.
/// </summary>
/// <typeparam name="TEntity">The workflow-stateful aggregate type.</typeparam>
/// <typeparam name="TState">The workflow state enum.</typeparam>
/// <param name="definition">The workflow definition for <typeparamref name="TState"/>.</param>
public abstract class WorkflowTransitionApplierBase<TEntity, TState>(IWorkflowDefinition<TState> definition)
    : IWorkflowTransitionApplier
    where TEntity : class
    where TState : struct, Enum
{
    /// <summary>The workflow definition used for FSM-legality validation.</summary>
    protected IWorkflowDefinition<TState> Definition { get; } = definition;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="targetState"/> is not a valid <typeparamref name="TState"/>
    /// value, when the entity is not found (see <see cref="OnEntityNotFoundAsync"/>), or when
    /// the current → target transition is not defined (the entity state likely changed since
    /// the transition was scheduled).
    /// </exception>
    public async Task ApplyAsync(Guid entityId, string targetState, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetState);

        if (!Enum.TryParse(targetState, out TState target))
        {
            throw new InvalidOperationException(
                $"'{targetState}' is not a valid {typeof(TState).Name} value for a scheduled transition of {typeof(TEntity).Name} '{entityId}'.");
        }

        TEntity? entity = await LoadAsync(entityId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            await OnEntityNotFoundAsync(entityId, cancellationToken).ConfigureAwait(false);
            return;
        }

        TState current = GetCurrentState(entity);

        // Idempotent: a concurrent dispatch or catch-up re-run that finds the entity already
        // in the target state is a no-op, not an error.
        if (EqualityComparer<TState>.Default.Equals(current, target))
        {
            return;
        }

        // System-context validation: FSM legality only. Never re-check user permissions here —
        // the transition was authorized at scheduling time and there is no user at execution time.
        bool isDefined = Definition.GetAllowedTransitions(current)
            .Any(t => EqualityComparer<TState>.Default.Equals(t.To, target));

        if (!isDefined)
        {
            throw new InvalidOperationException(
                $"Transition {current} -> {target} is not defined for {typeof(TEntity).Name} '{entityId}'. "
                + "The entity state may have changed since the transition was scheduled.");
        }

        SetState(entity, target);
        await SaveAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads the aggregate by identifier, or returns <c>null</c> when it no longer exists.</summary>
    protected abstract Task<TEntity?> LoadAsync(Guid entityId, CancellationToken cancellationToken);

    /// <summary>Returns the entity's current workflow state.</summary>
    protected abstract TState GetCurrentState(TEntity entity);

    /// <summary>
    /// Applies <paramref name="target"/> to the entity via its behaviour method
    /// (e.g. <c>Publish()</c> or <c>SetLifecycleStatus(...)</c>) — not by mutating a raw setter.
    /// </summary>
    protected abstract void SetState(TEntity entity, TState target);

    /// <summary>Persists the transitioned entity, letting the workflow interceptor record the audit trail.</summary>
    protected abstract Task SaveAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// Called when the entity is not found at execution time (e.g. deleted after scheduling).
    /// The default throws, surfacing the anomaly as a scheduled-action failure; override to
    /// treat a missing entity as a silent no-op.
    /// </summary>
    /// <param name="entityId">The identifier of the missing entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected virtual Task OnEntityNotFoundAsync(Guid entityId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            $"{typeof(TEntity).Name} '{entityId}' was not found for its scheduled workflow transition.");
}
