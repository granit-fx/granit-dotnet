---
title: "ADR-046: Activities vs Timeline split"
description: "Granit splits its collaborative layer into two peer modules with orthogonal concerns. Granit.Timeline holds the past — audit log, comments, attachments, followers, reactions, @-mentions. The new Granit.Activities holds the future — to-dos with deadlines and assignees, polymorphic across entities. Past vs future. Passive narrative vs action required. Notification to followers vs notification to assignees. Same shape as Odoo's mail.thread vs mail.activity split — without the inheritance mess."
sidebar:
  order: 46
  label: "046 - Activities vs Timeline"
---

> **Date:** 2026-04-30
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (`Granit.Timeline`, future `Granit.Activities`)
> **Epic:** [#1506](https://github.com/granit-fx/granit-dotnet/issues/1506) — Refonte UI Hybride
> **Story:** [#1525](https://github.com/granit-fx/granit-dotnet/issues/1525) — ADR Activities vs Timeline split
> **Status:** Accepted

## Context

`Granit.Timeline` already ships ([audit per #1529](https://github.com/granit-fx/granit-dotnet/issues/1529)) with the **passive collaborative feed**: chronological stream of audit events, comments, internal notes, attachments, followers (`ITimelineFollowerService`), notifications to followers (`ITimelineNotifier`). Reactions and `@`-mentions are scheduled additions ([Phase 2 stories under #1510](https://github.com/granit-fx/granit-dotnet/issues/1510)).

The Refonte UI Hybride initiative ([#1506](https://github.com/granit-fx/granit-dotnet/issues/1506)) needs the **active collaborative layer** — to-do items with deadlines and assignees, addressable from any entity (call this customer; meet that supplier; submit that report). Three reference frameworks shape the split:

- **Odoo** ships `mail.thread` (the chatter — past activity feed) and `mail.activity` (future to-dos) as separate mixins. Same conceptual split. Implementation entangles them through the `mail.message` table and `_inherits` chains; tenant-mode customizations break the boundary regularly.
- **Frappe** has a unified `Comment` doctype and a separate `ToDo` doctype. The boundary is clean conceptually but the absence of polymorphic cross-doctype activities means each app reinvents "activity for X" plumbing.
- **Salesforce** has the `Activity` polymorphic object with subtypes (Task, Event, Email). The polymorphism is the right call; the platform tax is too high to copy directly.

Granit's planning conversation locked the synthesis: **two peer modules**, not one with a flag. Past in `Granit.Timeline`; future in a new `Granit.Activities` module. Polymorphic cross-entity by default. Different notification semantics. Different UI affordances.

The naming question (`Granit.Timeline` vs `Granit.Chatter`) was answered in the planning conversation: **keep `Granit.Timeline`**. "Chatter" is too Odoo-flavoured; "Timeline" describes the data shape (chronological feed of heterogeneous events) accurately and is already adopted.

## Decision

### 1. Two peer modules with orthogonal concerns

| Aspect | `Granit.Timeline` | `Granit.Activities` (new) |
| ------ | ----------------- | ------------------------- |
| **Tense** | Past | Future (or overdue) |
| **Action required** | Passive narrative — read, optionally contribute | Active — assignee must do something |
| **Primary actors** | Followers (subscribed to the entity) | Assignee (single user, optionally a delegated approver) |
| **Notifications** | To followers, on every entry that crosses their threshold | To the assignee, on assignment + due-date reminders + overdue escalation |
| **UI surface** | Side panel "Activity log" on the entity detail | Side panel "Upcoming activities" on the entity detail; cross-entity calendar view; right-hand "My open activities" shortcut bar |
| **Workflow tie-in** | A workflow transition emits a `SystemLog` timeline entry | A workflow transition can `CreatesActivity(...)` to assign a follow-up to the next-state owner |
| **Storage** | Existing `Granit.Timeline.EntityFrameworkCore` schema | New `Granit.Activities.EntityFrameworkCore` schema |
| **Cross-entity polymorphism** | Already polymorphic (`(EntityType, EntityId)` pair) | Same polymorphic shape |

The two modules are **not coupled to each other**. An app can adopt one without the other. They can both light up on the same entity (and typically will: a Customer detail page shows both a past activity log and upcoming activities).

### 2. `Granit.Activities` shape

```csharp
public sealed class Activity : AggregateRoot
{
    Guid Id;
    string EntityType;          // polymorphic FK target (e.g. "Granit.Parties.Party")
    Guid EntityId;
    string Type;                // extensible enum — see §4
    Guid AssignedToUserId;
    Guid? CreatedByUserId;
    DateTimeOffset DueAt;
    string? Description;
    ActivityStatus Status;      // Open, Done, Cancelled (Overdue computed from DueAt + Status)
    DateTimeOffset? CompletedAt;
    Guid? CompletedByUserId;
    // + audit, multi-tenant, soft-delete via standard Granit conventions
}

public enum ActivityStatus { Open, Done, Cancelled }
```

The polymorphic `(EntityType, EntityId)` pair lets any entity host activities without coupling — same pattern as `Granit.Timeline`.

### 3. `EntityDefinition.Activities()` — opt-in per entity

An `EntityDefinition` opts into activities and can declare smart defaults:

```csharp
public sealed class CustomerEntityDefinition : EntityDefinition<Customer>
{
    protected override void Configure(EntityDefinitionBuilder<Customer> b)
    {
        b.Activities(a => a
            .Enabled()
            .AllowedTypes("Call", "Meeting", "Email", "ToDo")
            .DefaultAssignee(c => c.AccountManagerUserId));

        b.Detail.SidePanel.Activities();   // surfaces the Activities side panel on the detail
    }
}
```

The `AllowedTypes` filter lets an entity restrict the activity-type catalog (e.g. `Customer` allows Call/Meeting/Email/ToDo but not `Onboarding`, which only `Identity.User` accepts).

### 4. Activity types — extensible via `IActivityTypeProvider`

The framework ships a small starter catalog (`ToDo`, `Call`, `Meeting`, `Email`); modules add domain-specific types via the IoC contributor pattern of [ADR-045](./045-contributor-pattern):

```csharp
// In Granit.Sales
public sealed class SalesActivityTypeProvider : IActivityTypeProvider
{
    public IEnumerable<ActivityType> Provide() =>
    [
        new ActivityType("Quote", icon: "file-text", displayKey: "Activity:Quote"),
        new ActivityType("Demo",  icon: "monitor",   displayKey: "Activity:Demo"),
    ];
}

// Module DI
services.AddActivityTypeProvider<SalesActivityTypeProvider>();
```

Each `ActivityType` carries a name, an icon, an i18n display key, and an optional `DefaultDurationMinutes`. Types from missing modules silently drop (per ADR-045 resolution rules) — an entity that allows `"Quote"` but `Granit.Sales` isn't loaded simply doesn't surface the option.

### 5. Notification semantics — orthogonal

| Event | `Granit.Timeline` | `Granit.Activities` |
| ----- | ----------------- | ------------------- |
| New comment / system event | Notify all followers (existing `ITimelineNotifier`) | — |
| Activity assigned | — | Notify the assignee |
| Activity due tomorrow | — | Reminder to the assignee |
| Activity overdue | — | Escalation to the assignee + optional manager (configurable) |
| `@`-mention in a comment (Phase 2) | Targeted notification to the mentioned user | — |
| Reaction added (Phase 2) | Notify the comment author | — |

Both modules emit through `Granit.Notifications` channels — the user receives one consolidated stream, but the framework keeps the producers cleanly separate.

### 6. UI affordances — orthogonal

The Granit React shell renders both modules on an entity detail page, in **distinct slots**:

- **Side panel: Timeline** — chronological list of past events. Read-mostly. Compose box at top for new comments / internal notes / attachments.
- **Side panel: Activities** — list of upcoming + overdue activities for this entity. Action-mostly. "Add activity" button at top; status toggles on each item.
- **Detail header: Activities badge** — shows `(2 due today, 1 overdue)` with click-through to the side panel.
- **Cross-entity surface (`Granit.Activities.Endpoints`)** — calendar view of all my activities across entities; the right-hand shortcut panel (Phase 2 / [ADR-044](./044-workspace-navigation) §7 follow-up) shows "My open activities" / "Due today" / "Overdue".

Timeline shows up below activities on the detail page (because users typically want to act on the future before reviewing the past).

### 7. Workflow integration

A workflow transition can declaratively create an activity for the next-state assignee:

```csharp
b.Workflow(wf => wf
    .Transition(InvoiceState.Draft, InvoiceState.Submitted, t => t
        .RequiresPermission("Invoicing.Invoices.Submit")
        .CreatesActivity(a => a
            .Type("Approve")
            .Assignee(invoice => invoice.NextApproverUserId)
            .DueIn(TimeSpan.FromDays(2))
            .Description("Approve submitted invoice"))));
```

The framework wires up the activity creation at transition commit time. Both Timeline (transition logged) and Activities (to-do assigned) light up — the consumer sees both, properly attributed to the same transition event.

### 8. What stays in `Granit.Timeline` (not duplicated)

- Comments / Internal notes / System log (already shipped)
- Attachments via `Granit.BlobStorage` (already shipped)
- Followers + subscriptions (`ITimelineFollowerService`, already shipped)
- `@`-mentions (Phase 2)
- Reactions (Phase 2)
- Reply-by-email (Phase 4)

No naming change to `Granit.Timeline`. The audit ([#1529](https://github.com/granit-fx/granit-dotnet/issues/1529)) confirmed the existing module covers the past-feed needs; the only future-tense extension Phase 2 brings is `@`-mentions and reactions.

## Consequences

### Positive

- Clean conceptual boundary — past vs future, passive vs active. Devs and end users learn the split once and apply it everywhere.
- Two modules, two release cadences. `Granit.Activities` can ship Phase 2 without touching the existing `Granit.Timeline` shipped surface.
- Polymorphic cross-entity activities by default — no per-app plumbing for "activity for X."
- Workflow integration via `CreatesActivity(...)` is one declarative line — Frappe and Odoo both require multiple lines of glue per transition.
- Notification semantics are orthogonal and obvious: followers see the past, assignees see the future. No more "why am I getting notified about this comment when I asked to track activities?"

### Negative / accepted trade-offs

- Two modules to install when an app wants both. Mitigated by the `Granit.Bundle.Collaboration` (planned post-Phase 2) which pulls both with sensible defaults.
- The activity-type catalog is extensible only via compiled C# (per ADR-045). Tenant admins cannot add a custom type from the UI — that's a Tier B Layer 2 surface (Phase 4 / `Granit.Entities.Customization.Fields`) we deliberately don't open here.
- Workflow `.CreatesActivity(...)` ties workflow definitions to the activities module's contract. Mitigated by the `Granit.Workflow.Abstractions` split (#1518) — only the abstraction is referenced; runtime activities module remains optional at the workflow declaration site.

## Cross-references

- [ADR-040](./040-three-tier-metadata-architecture) — Three-tier metadata. Both modules' definitions are Tier A; tenant admins cannot author activity types.
- [ADR-045](./045-contributor-pattern) — IoC contributor pattern. `IActivityTypeProvider` is one of the three primitives this pattern serves.
- [#1529](https://github.com/granit-fx/granit-dotnet/issues/1529) — Phase 0 audit of `Granit.Timeline` (planned). Confirms the existing surface vs the Phase 2 gaps (`@`-mentions, reactions).

## References

- Odoo `mail.thread` vs `mail.activity` — the conceptual split is the same; Odoo's implementation entangles them through `_inherits` chains and `mail.message` storage. Granit deliberately keeps the two module-level (no shared aggregate, no inheritance).
- Frappe `Comment` + `ToDo` doctypes — separate but no polymorphic cross-doctype shape; each app reinvents "activity for X." Granit's polymorphic `(EntityType, EntityId)` solves this in the framework.
- Salesforce `Activity` with subtypes (Task, Event, Email) — closer to Granit's shape; we adopt the polymorphism without the platform tax.
- Planning conversation reasoning — captured in PR #1599 and the underlying conversation: keep `Granit.Timeline` name (Chatter too Odoo-flavoured); split Activities as a peer module rather than a Timeline mode.
