namespace Granit.Activities.Domain;

/// <summary>
/// Lifecycle states of an <see cref="Activity"/> (ADR-046 §2). Three terminal
/// states only — <c>Overdue</c> is a computed view (<c>DueAt &lt; clock.Now &amp;&amp; Status == Open</c>),
/// never persisted, so the state machine stays small enough to reason about
/// without a full workflow diagram.
/// </summary>
public enum ActivityStatus
{
    /// <summary>Default state — assigned, not yet completed or cancelled.</summary>
    Open = 0,

    /// <summary>Completed by the assignee (or by an admin acting on their behalf).</summary>
    Done = 1,

    /// <summary>Cancelled before completion — the activity is no longer relevant.</summary>
    Cancelled = 2,
}
