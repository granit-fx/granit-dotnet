namespace Granit.Workflow;

/// <summary>
/// Fluent builder for configuring a single <see cref="WorkflowTransition{TState}"/>.
/// </summary>
/// <typeparam name="TState">Enum type representing the workflow states.</typeparam>
public sealed class TransitionBuilder<TState> where TState : struct, Enum
{
    private readonly TState _from;
    private readonly TState _to;
    private string? _name;
    private string? _requiredPermission;
    private bool _requiresApproval;

    internal TransitionBuilder(TState from, TState to)
    {
        _from = from;
        _to = to;
    }

    /// <summary>Sets the display name for this transition.</summary>
    public TransitionBuilder<TState> Named(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>Sets the permission required to execute this transition.</summary>
    public TransitionBuilder<TState> RequiresPermission(string permission)
    {
        _requiredPermission = permission;
        return this;
    }

    /// <summary>
    /// Enables approval routing: when the user lacks the required permission,
    /// the transition routes to a pending review state and notifies approvers.
    /// </summary>
    public TransitionBuilder<TState> RequiresApproval()
    {
        _requiresApproval = true;
        return this;
    }

    internal WorkflowTransition<TState> Build() =>
        new()
        {
            From = _from,
            To = _to,
            Name = _name,
            RequiredPermission = _requiredPermission,
            RequiresApproval = _requiresApproval,
        };
}
