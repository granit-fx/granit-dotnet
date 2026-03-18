---
title: "Mediator Pattern — In-Process Command Routing"
description: "Route commands, queries, and events through Wolverine as a central in-process mediator — eliminates direct service dependencies and enables cross-cutting pipeline behaviors."
sidebar:
  label: Mediator
  order: 22
---

## Definition

The Mediator pattern centralizes interactions between components via an
intermediary object. Components do not communicate directly; they send
messages to the mediator, which routes them to the appropriate recipients.

In Granit, **Wolverine** is the central mediator: it routes commands, events,
and queries to handlers, manages transactions, retries, and the Outbox.

## Diagram

```mermaid
flowchart TD
    H1[HTTP Handler] -->|command| W[Wolverine Bus]
    H2[Background Job] -->|command| W
    H3[Event Publisher] -->|event| W

    W -->|route| C1[CommandHandler A]
    W -->|route| C2[CommandHandler B]
    W -->|route| E1[EventHandler X]
    W -->|route| E2[EventHandler Y]

    W -->|manages| TX[Transactions]
    W -->|manages| RT[Retries]
    W -->|manages| OB[Outbox]
    W -->|manages| DLQ[Dead Letter Queue]

    style W fill:#4a9eff,color:#fff
```

## Implementation in Granit

| Component | File | Role |
|-----------|------|------|
| `GranitWolverineModule` | `src/Granit.Wolverine/GranitWolverineModule.cs` | Mediator configuration |
| `AddGranitWolverine()` | `src/Granit.Wolverine/Extensions/WolverineHostApplicationBuilderExtensions.cs` | Registers policies (retry, validation, DLQ) |
| `WolverineMessagingOptions` | `src/Granit.Wolverine/WolverineMessagingOptions.cs` | Retry delays: 5s, 30s, 5min |

### Policies managed by the mediator

- **FluentValidation**: invalid messages go to DLQ immediately
- **Exponential retry**: `ValidationException` goes to DLQ, others retry at 5s/30s/5min
- **Outbox**: `IIntegrationEvent` messages persisted atomically
- **Local queue**: `IDomainEvent` messages processed in-process

## Rationale

Without a mediator, each handler would need to know which other handlers to
call and manage its own transactions and retries. Wolverine centralizes this
logic and keeps handlers pure and testable.

## Usage example

```csharp
// Handlers do not know each other -- Wolverine routes messages
public static class CreatePatientHandler
{
    public static IEnumerable<object> Handle(
        CreatePatientCommand cmd,
        AppDbContext db)
    {
        Patient patient = new() { /* ... */ };
        db.Patients.Add(patient);

        // Wolverine routes to the correct handler automatically
        yield return new PatientCreatedOccurred { PatientId = patient.Id };
        yield return new SendWelcomeEmailCommand { Email = cmd.Email };
    }
}
// PatientCreatedOccurred -> local queue -> domain handler (same tx)
// SendWelcomeEmailCommand -> Outbox -> background handler (after commit)
```

## Further reading

- [Mediator -- refactoring.guru](https://refactoring.guru/design-patterns/mediator)
