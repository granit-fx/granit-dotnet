using Granit.Exceptions;
using Granit.Templating.Store;

namespace Granit.Templating.Exceptions;

/// <summary>
/// Exception thrown when <see cref="ITemplateTransitionHook.CanTransitionAsync"/>
/// rejects a lifecycle transition.
/// Maps to <c>409 Conflict</c> via <see cref="ConflictException"/>.
/// </summary>
public sealed class TemplateTransitionDeniedException : ConflictException
{
    /// <summary>The status the transition was attempted from.</summary>
    public TemplateLifecycleStatus From { get; }

    /// <summary>The status the transition was attempted to.</summary>
    public TemplateLifecycleStatus To { get; }

    /// <summary>
    /// Initializes a new <see cref="TemplateTransitionDeniedException"/>.
    /// </summary>
    /// <param name="from">Current lifecycle status.</param>
    /// <param name="to">Target lifecycle status.</param>
    public TemplateTransitionDeniedException(TemplateLifecycleStatus from, TemplateLifecycleStatus to)
        : base(
            "Template:TransitionDenied",
            $"Template lifecycle transition from '{from}' to '{to}' was denied by the transition hook.")
    {
        From = from;
        To = to;
    }
}
