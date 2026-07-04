# Granit.Workflow.Scheduling

Time-based workflow transitions as a reusable framework primitive. Bridges
[`Granit.Workflow`](../Granit.Workflow) (FSM validation + ISO 27001 audit) and
[`Granit.Scheduling`](../Granit.Scheduling) (durable one-shot execution): schedule a
workflow-stateful entity to move from one state to another at a future instant, then cancel
or reschedule it per entity when the date changes.

## What it provides

- `ScheduledWorkflowTransitionPayload` — the persisted `IScheduledPayload`. Non-generic:
  the target state travels as a string and the concrete `TState` enum is resolved by an
  entity-specific applier.
- `IScheduledTransitionService` — minimal `ScheduleTransitionAsync` /
  `CancelScheduledTransitionAsync` / `RescheduleTransitionAsync`, keyed by
  `(workflowEntityType, entityId)` via a deterministic correlation id.
- `IWorkflowTransitionApplier` (+ `WorkflowTransitionApplierBase<TEntity, TState>`) —
  loads the aggregate, validates FSM legality, sets the status and saves. One per entity type.
- `ScheduledWorkflowTransitionHandler` — Wolverine handler that dispatches a scheduled
  transition to the right applier.

Audit (`WorkflowTransitionRecord`) and `WorkflowStateChangedEvent` are produced automatically
by the workflow EF Core interceptor on save. Atomic single-execution, retry and status
transitions come from `Granit.Scheduling.Wolverine`.

## System-context execution

Scheduled transitions run with **no authenticated user**. Authorization happens once, at
scheduling time (the scheduling endpoint is permission-gated). At execution time the applier
validates only the **FSM legality** of the transition against `IWorkflowDefinition<TState>` and
does **not** re-evaluate user permissions — the scheduling user may have been revoked and the
catch-up safety net carries no user. `WorkflowTransitionApplierBase<TEntity, TState>` encodes
this decision.

## Usage

```csharp
// Registration
services.AddWorkflowScheduling();
services.AddWorkflowTransitionApplier<BlogPostTransitionApplier>("BlogPost");

// Schedule publication at an absolute instant (convert wall time + IANA zone with Granit.Timing)
await scheduledTransitions.ScheduleTransitionAsync(
    workflowEntityType: "BlogPost",
    entityId: post.Id,
    targetState: nameof(BlogPostStatus.Published),
    executeAt: publishAtUtc,
    cancellationToken);

// Editor moved the date
await scheduledTransitions.RescheduleTransitionAsync(
    "BlogPost", post.Id, nameof(BlogPostStatus.Published), newPublishAtUtc, cancellationToken);
```

An applier derives from the base and supplies the four entity-specific steps:

```csharp
internal sealed class BlogPostTransitionApplier(
    BlogDbContext db,
    IWorkflowDefinition<BlogPostStatus> definition)
    : WorkflowTransitionApplierBase<BlogPost, BlogPostStatus>(definition)
{
    protected override Task<BlogPost?> LoadAsync(Guid id, CancellationToken ct) =>
        db.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, ct);

    protected override BlogPostStatus GetCurrentState(BlogPost post) => post.Status;

    protected override void SetState(BlogPost post, BlogPostStatus target) => post.TransitionTo(target);

    protected override Task SaveAsync(BlogPost post, CancellationToken ct) => db.SaveChangesAsync(ct);
}
```
