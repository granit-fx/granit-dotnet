namespace Granit.Workflow;

/// <summary>
/// Immutable FSM workflow definition built via <see cref="WorkflowDefinitionBuilder{TState}"/>.
/// Thread-safe and designed to be stored as a singleton or static field.
/// </summary>
/// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
public sealed class WorkflowDefinition<TState> : IWorkflowDefinition<TState>
    where TState : struct, Enum
{
    private readonly Dictionary<TState, List<WorkflowTransition<TState>>> _transitionsBySource;

    internal WorkflowDefinition(
        TState initialState,
        IReadOnlyList<WorkflowTransition<TState>> transitions)
    {
        InitialState = initialState;
        Transitions = transitions;
        _transitionsBySource = transitions
            .GroupBy(t => t.From)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <inheritdoc/>
    public TState InitialState { get; }

    /// <inheritdoc/>
    public IReadOnlyList<WorkflowTransition<TState>> Transitions { get; }

    /// <inheritdoc/>
    public IReadOnlyList<WorkflowTransition<TState>> GetAllowedTransitions(TState from) =>
        _transitionsBySource.TryGetValue(from, out List<WorkflowTransition<TState>>? transitions)
            ? transitions
            : [];

    /// <summary>
    /// Creates a new workflow definition using the fluent builder API.
    /// </summary>
    /// <param name="configure">Builder configuration delegate.</param>
    /// <returns>An immutable <see cref="WorkflowDefinition{TState}"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the definition is invalid (no initial state, no transitions,
    /// duplicate transitions, or unreachable states).
    /// </exception>
#pragma warning disable CA1000 // Intentional: factory method on generic type for fluent API ergonomics
    public static WorkflowDefinition<TState> Create(
        Action<WorkflowDefinitionBuilder<TState>> configure)
#pragma warning restore CA1000
    {
        WorkflowDefinitionBuilder<TState> builder = new();
        configure(builder);
        return builder.Build();
    }
}
