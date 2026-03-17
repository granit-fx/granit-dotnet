---
title: "State Machine"
description: "Explicit state transitions for idempotency and blob lifecycle in Granit"
sidebar:
  order: 25
---

## Definition

The State Machine pattern models an object whose behavior changes based on
its internal state. Transitions between states are explicit and controlled,
preventing invalid states.

Granit uses two state machines: one for HTTP idempotency, the other for the
blob lifecycle.

## Diagram

```mermaid
stateDiagram-v2
    state "Idempotency" as I {
        [*] --> Absent
        Absent --> InProgress : Lock acquired (SET NX)
        InProgress --> Completed : Handler succeeds
        InProgress --> Absent : Handler fails (5xx)
        Completed --> [*] : Replay response
    }

    state "BlobStatus" as B {
        [*] --> Pending : InitiateUploadAsync()
        Pending --> Uploading : MarkAsUploading()
        Uploading --> Valid : MarkAsValid()
        Uploading --> Rejected : MarkAsRejected()
        Valid --> Deleted : MarkAsDeleted()
        Rejected --> Deleted : MarkAsDeleted()
    }
```

## Implementation in Granit

### IdempotencyState

| Component | File |
|-----------|------|
| `IdempotencyState` | `src/Granit.Http.Idempotency/Models/IdempotencyState.cs` |
| `IdempotencyMiddleware` | `src/Granit.Http.Idempotency/Internal/IdempotencyMiddleware.cs` |

States: `Absent` -> `InProgress` -> `Completed`

### BlobStatus

| Component | File |
|-----------|------|
| `BlobStatus` | `src/Granit.BlobStorage/BlobStatus.cs` |
| `BlobDescriptor` | `src/Granit.BlobStorage/BlobDescriptor.cs` |

States: `Pending` -> `Uploading` -> `Valid`/`Rejected` -> `Deleted`

Transitions are encapsulated in methods on `BlobDescriptor`:
`MarkAsUploading()`, `MarkAsValid()`, `MarkAsRejected()`, `MarkAsDeleted()`.
Each method validates the current state before the transition.

## Rationale

State machines make transitions explicit and testable. A blob cannot go from
`Pending` to `Deleted` directly -- it must traverse intermediate states.
Idempotency uses states to manage concurrency (InProgress -> 409 Conflict).

## Usage example

```csharp
// Transitions are secured by typed methods
BlobDescriptor descriptor = new() { Status = BlobStatus.Pending };

descriptor.MarkAsUploading();  // Pending -> Uploading
descriptor.MarkAsValid();      // Uploading -> Valid
descriptor.MarkAsDeleted(clock.Now, "GDPR Art. 17"); // Valid -> Deleted

// Invalid transition -> exception
descriptor.MarkAsValid(); // Deleted -> Valid: InvalidOperationException
```

## Further reading

- [State -- refactoring.guru](https://refactoring.guru/design-patterns/state)
