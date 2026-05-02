# Granit.Activities

Runtime module for the cross-entity polymorphic to-do system specified by
[ADR-046][adr-046] — the **future** half of Granit's collaborative layer
(passive past = `Granit.Timeline`, active future = `Granit.Activities`).

Pull this from hosts that **resolve and persist** activities. Module-side
declarations (cross-module `IActivityTypeProvider`, `EntityDefinition.Activities()`
opt-in) only need the lighter `Granit.Activities.Abstractions` package.

## What's inside

| Type | Role |
| ---- | ---- |
| `Activity` | Aggregate root, polymorphic on `(EntityType, EntityId)` — append-only state machine (`Open` → `Done` / `Cancelled`) |
| `ActivityStatus` | Three terminal states — `Overdue` is computed (`DueAt < clock.Now && Status == Open`), never persisted |
| `ActivityAssignedEvent` / `ActivityCompletedEvent` / `ActivityCancelledEvent` / `ActivityReassignedEvent` / `ActivityRescheduledEvent` | Local-bus domain events |
| `ActivityAssignedEto` / `ActivityCompletedEto` | Wolverine outbox integration events |
| `ActivityRegistry` | `IActivityRegistry` impl — aggregates every registered `IActivityTypeProvider` at construction; fails fast on duplicate names |
| `AddGranitActivities()` | DI extension — registers the registry + the framework's `StandardActivityTypeProvider` |

## Composition example

```csharp
// In the host's Program.cs
builder.Services
    .AddGranitActivities()                                // registry + standard catalog
    .AddActivityTypeProvider<SalesActivityTypeProvider>() // domain-specific types from another module
    .AddActivityTypeProvider<IdentityActivityTypeProvider>();
```

Persistence (DbContext, EF configurations) ships in `Granit.Activities.EntityFrameworkCore`
(story A3); HTTP endpoints in `Granit.Activities.Endpoints` (story A4).

## Append-only semantics

Activities never re-open. `Complete` and `Cancel` are terminal — calling
`Reassign` / `Reschedule` / `Complete` / `Cancel` on an already-terminal
activity throws. If a follow-up is needed, create a new activity.

This is intentional and ADR-locked: a re-open transition would require a
state machine richer than two leaves, would muddy the audit trail, and would
make notification semantics ambiguous (does re-opening alert the original
assignee or the current one?). Append-only keeps the model simple and the
audit trail unambiguous.

[adr-046]: ../../docs-site/src/content/docs/dotnet/architecture/adr/046-activities-vs-timeline.md
