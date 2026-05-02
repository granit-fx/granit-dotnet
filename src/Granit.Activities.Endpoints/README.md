# Granit.Activities.Endpoints

Minimal API surface for [Granit.Activities](../Granit.Activities/README.md) —
list / get / create / complete / cancel / reassign / reschedule activities,
with the framework's permission gates and FluentValidation auto-discovery.

## Wire-up

```csharp
// In the host's Program.cs
builder.Services
    .AddGranitActivities()                         // registry + standard catalog (story A2)
    .AddGranitActivitiesEntityFrameworkCore(opts =>
        opts.UseNpgsql(builder.Configuration.GetConnectionString("Default"))); // story A3 — wires Reader + Writer

// after AddRouting
app.MapGranitActivities();
```

## Routes

| Method | Path | Permission | Returns |
| ------ | ---- | ---------- | ------- |
| `GET` | `/api/activities` | `Activities.Activities.Read` | `ActivityListResponse` (paginated) |
| `GET` | `/api/activities/{id}` | `Activities.Activities.Read` | `ActivityResponse` |
| `POST` | `/api/activities` | `Activities.Activities.Manage` | `ActivityResponse` (201 Created) |
| `POST` | `/api/activities/{id}/complete` | `Activities.Activities.Execute` | `ActivityResponse` |
| `POST` | `/api/activities/{id}/cancel` | `Activities.Activities.Manage` | `ActivityResponse` |
| `PUT` | `/api/activities/{id}/assignee` | `Activities.Activities.Manage` | `ActivityResponse` |
| `PUT` | `/api/activities/{id}/due-date` | `Activities.Activities.Manage` | `ActivityResponse` |

## List filters (query string)

- `entityType` — polymorphic FK (e.g. `"Granit.Parties.Party"`)
- `entityId` — polymorphic FK row id
- `assignedToUserId` — only this user's activities
- `status` — `OpenOrOverdue` (default for the inbox), `Done`, `Cancelled`
- `dueAtFrom` / `dueAtTo` — half-open window
- `page` / `pageSize` — defaults 1 / 20, capped at `ActivitiesEndpointsOptions.MaxPageSize` (default 100)

## Append-only contract

Per [ADR-046 §2](../../docs-site/src/content/docs/dotnet/architecture/adr/046-activities-vs-timeline.md),
activities never re-open. `Complete` / `Cancel` / `Reassign` / `Reschedule` on
an already-terminal activity return `409 Conflict` with the aggregate's error
message. Create a new activity if a follow-up is needed.

## Out of scope

- Cross-entity calendar (`GET /api/activities/calendar`) — story A6
- Notifications / email — story A7
- Background reminders / overdue scan — story A8
- `EntityDefinition.Activities()` opt-in — story A5
