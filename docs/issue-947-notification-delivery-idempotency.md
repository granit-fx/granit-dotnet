# GH #947 — Notification delivery audit & duplicate-send prevention

## Context: open GitHub issues (not labeled `Type: Story`)

Representative backlog items excluding `Type: Story` included **BUG #947**, feature/epic planning issues, spikes, and tech-debt tracks. Among those, **[#947](https://github.com/granit-fx/granit-dotnet/issues/947)** stood out because it couples user-visible correctness (duplicate emails), durable audit semantics, concurrency, and testability.

This document describes the defect, how it was addressed in `granit-dotnet`, automated tests added, and the small sample verifier that exercises the scenario end-to-end.

## Problem (#947 summary)

Outbound notification delivery persisted the ISO 27001 delivery audit (`notifications_delivery_attempts`) **after** sending through the channel.

Two failure modes compounded:

1. **Retry amplification** — Infrastructure that retries failures when persistence throws (`DbUpdateException` such as PostgreSQL `23505` duplicate key) could re-run the SMTP path even though the outbound message already succeeded once.
2. **Concurrent dispatch** — Two workers racing on the **same logical `delivery_id`** could both pass **“not yet delivered successfully”**, both send SMTP, then fight on the INSERT — producing duplicate inbox noise even when retries were benign.

Mitigating only retries (swallow persistence errors post-send) closes (1) but not (2).

## Implemented solution

Delivery audit writes now follow **claim-before-send**:

1. **`TryAcquireDeliveryAttemptAsync`** — Inserts one row keyed by unique `delivery_id`, with **`IsSuccess = null`** (in-flight sentinel). Duplicate insert → unique violation → classify outcome:
   - If the persisted row reflects a **prior channel failure**, it is transitioned back to in-flight (**resume for transport retry**) by clearing the terminal failure state.
   - If the persisted row reflects **successful delivery**, or another worker genuinely owns an in-flight claim, **`false`** is returned → **sender exits without SMTP**.
2. **Channel send / failure handling** — Same as prior behavior (instrumentation + `NotificationDeliveryException` when the channel blows up).
3. **`CompleteDeliveryAttemptAsync`** — Terminates audit fields (`bool` success vs failure, durations, captured error strings). Persist errors after SMTP success remain **non‑fatal**: they never rethrow upstream to avoid Wolverine/host retry duplication.

Supporting changes:

| Area | Detail |
| ---- | ------ |
| `NotificationDeliveryAttempt.IsSuccess` | `bool?`: `null = in-flight`, `true/false = terminal outcome` |
| `INotificationDeliveryWriter` | `TryAcquireDeliveryAttemptAsync` + `CompleteDeliveryAttemptAsync` replace `RecordAsync` |
| `NotificationDeliveryHandler` | Claims before send, logs concurrency skips separately, guarantees cancellation finalizes audits when a row was already claimed |

## Automated tests (recommended)

Prefer building the **Infrastructure** shard subset (Roslyn‑friendly CI slice):

```bash
dotnet build .github/shard-filters/infrastructure.slnf
dotnet test tests/Granit.Notifications.Tests --no-build
dotnet test tests/Granit.Notifications.EntityFrameworkCore.Tests --no-build
```

Key coverage highlights:

| Test focus | Suite |
| ---------- | ----- |
| Channel miss / successes / failures / context mapping | `tests/Granit.Notifications.Tests/NotificationDeliveryHandler*.cs` |
| Cancellation finalizes audits | `HandleAsync_OperationCanceledException_is_not_caught` |
| Claim / finalize / retention purge / resume-after-failure | `tests/Granit.Notifications.EntityFrameworkCore.Tests/EfCoreNotificationDeliveryStoreTests.cs` |
| **Parallel concurrency** SQLite end-to-end | `NotificationDeliveryHandlerConcurrencyTests.Parallel_same_delivery_parallel_handlers_sends_email_once` |

## Sample verifier application

`samples/Issue947.NotificationDeliveryVerify/` is an executable wired like a minimal Granit API host subset:

| Concern | How the verifier handles it |
| ------- | --------------------------- |
| Storage | Deletes `issue947-verify.db` best-effort, recreates SQLite schema via EF `EnsureCreated` |
| DI | `HostApplicationBuilder`: `AddGranitNotifications()`, replace `INotificationChannel` with counting email stub, SQLite EF module |
| Hosted noise | Drops `NotificationDispatchWorker` registrations by reflection substring match |
| Workload | Spawns **two scopes** concurrently → two `NotificationDeliveryHandler` instances share the identical `DeliverNotificationCommand` |
| Assertion | Prints email send counter; **`exit code 0` only when sends == `1`** |

Run manually:

```bash
dotnet run --project samples/Issue947.NotificationDeliveryVerify/Issue947.NotificationDeliveryVerify.csproj
```

Internals access to `NotificationsDbContext` is intentionally granted solely to **Issue947.NotificationDeliveryVerify** (see `<InternalsVisibleTo>` on `Granit.Notifications.EntityFrameworkCore`) — do not reuse that pattern broadly in production forks.

---

**Upstream issue:** `[BUG] Notification emails sent twice when delivery audit INSERT fails` — GitHub #947.
