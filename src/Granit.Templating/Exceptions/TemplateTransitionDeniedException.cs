using Granit.Exceptions;
using Granit.Workflow.Domain;

namespace Granit.Templating.Exceptions;

/// <summary>
/// Exception thrown when <see cref="Store.ITemplateTransitionHook.CanTransitionAsync"/>
/// rejects a lifecycle transition.
/// </summary>
public sealed class TemplateTransitionDeniedException : ConflictException
{
    /// <summary>The status the transition was attempted from.</summary>
    public WorkflowLifecycleStatus From { get; }

    /// <summary>The status the transition was attempted to.</summary>
    public WorkflowLifecycleStatus To { get; }

    public TemplateTransitionDeniedException(WorkflowLifecycleStatus from, WorkflowLifecycleStatus to)
        : base(
            "Template:TransitionDenied",
            $"Template lifecycle transition from '{from}' to '{to}' was denied by the transition hook.")
    {
        From = from;
        To = to;
    }
}
