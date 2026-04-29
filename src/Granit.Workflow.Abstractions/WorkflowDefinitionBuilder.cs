namespace Granit.Workflow;

/// <summary>
/// Fluent builder for constructing an immutable <see cref="WorkflowDefinition{TState}"/>.
/// Validates the graph at <see cref="Build"/> time: initial state must be set,
/// at least one transition must be defined, no duplicate transitions,
/// and warns about unreachable states.
/// </summary>
/// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
public sealed class WorkflowDefinitionBuilder<TState> where TState : struct, Enum
{
    private TState? _initialState;
    private readonly List<WorkflowTransition<TState>> _transitions = [];

    /// <summary>Sets the initial state for newly created entities.</summary>
    public WorkflowDefinitionBuilder<TState> InitialState(TState state)
    {
        _initialState = state;
        return this;
    }

    /// <summary>
    /// Adds a transition from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    public WorkflowDefinitionBuilder<TState> Transition(
        TState from,
        TState to,
        Action<TransitionBuilder<TState>>? configure = null)
    {
        TransitionBuilder<TState> transitionBuilder = new(from, to);
        configure?.Invoke(transitionBuilder);
        _transitions.Add(transitionBuilder.Build());
        return this;
    }

    /// <summary>
    /// Builds and validates the workflow definition.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the definition is invalid.
    /// </exception>
    internal WorkflowDefinition<TState> Build()
    {
        if (_initialState is null)
        {
            throw new InvalidOperationException(
                "Workflow definition must have an initial state. Call InitialState() before Build().");
        }

        if (_transitions.Count == 0)
        {
            throw new InvalidOperationException(
                "Workflow definition must have at least one transition.");
        }

        // Check for duplicate transitions (same From+To)
        (TState From, TState To)? duplicate = _transitions
            .GroupBy(t => (t.From, t.To))
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .Cast<(TState From, TState To)?>()
            .FirstOrDefault();

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate transition: {duplicate.Value.From} → {duplicate.Value.To}. " +
                "Each From+To pair must be unique.");
        }

        // Detect unreachable states (states with no incoming transition except initial state)
        HashSet<TState> allStates = [_initialState.Value];
        foreach (WorkflowTransition<TState> transition in _transitions)
        {
            allStates.Add(transition.From);
            allStates.Add(transition.To);
        }

        HashSet<TState> reachable = [_initialState.Value];
        // BFS from initial state using adjacency lookup
        ILookup<TState, TState> adjacency = _transitions.ToLookup(t => t.From, t => t.To);
        Queue<TState> queue = new();
        queue.Enqueue(_initialState.Value);

        while (queue.Count > 0)
        {
            TState current = queue.Dequeue();
            foreach (TState target in adjacency[current].Where(reachable.Add))
            {
                queue.Enqueue(target);
            }
        }

        HashSet<TState> unreachable = [.. allStates];
        unreachable.ExceptWith(reachable);

        if (unreachable.Count > 0)
        {
            string unreachableNames = string.Join(", ", unreachable);
            throw new InvalidOperationException(
                $"Unreachable states detected: {unreachableNames}. " +
                "These states have no incoming transition path from the initial state.");
        }

        return new WorkflowDefinition<TState>(_initialState.Value, _transitions.AsReadOnly());
    }
}
