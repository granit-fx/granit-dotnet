---
title: "ADR-033: Metering hybrid — lifecycle, CountDistinct, recompute, backfill, deprecate"
description: "Promote MeterDefinition to a workflow-managed lifecycle (Draft / Published / Archived), introduce CountDistinct aggregation with a typed JSON path, expose admin-side recompute / backfill / event-deprecation endpoints under a transaction-scoped advisory lock to keep aggregates consistent with concurrent ingestion."
sidebar:
  order: 33
  label: "033 - Metering Hybrid"
---

> **Date:** 2026-04-25
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.Metering`, `Granit.Metering.Abstractions`,
> `Granit.Metering.Endpoints`, `Granit.Metering.EntityFrameworkCore`,
> `Granit.Metering.BackgroundJobs`

## Context

`Granit.Metering` shipped as a fast-ingestion engine: events land in
`MeterEvent`, a background job rolls them up into `UsageAggregate` rows
keyed by `(MeterDefinitionId, Period, PeriodStart)`, and quotas /
invoices read from those rollups. The aggregate path was deliberately
single-direction — events flow forward, rollups flow forward, nothing
ever reaches back into the past.

Three workflows kept failing to fit that model:

1. **Late-arriving authoritative data.** Customer success teams
   occasionally need to ingest historical events (a prior month's CSV
   handed over post-onboarding). The 7-day age cap on the standard
   ingestion endpoint rejected these.
2. **Bug-driven reprocessing.** When a bug in upstream emission
   inflated a meter — say, a retry storm that double-counted API calls
   — there was no clean way to mark the bad events as discarded and
   rebuild the aggregate without surgically deleting rows by hand.
3. **Distinct-count metrics.** Active-users-per-month is a textbook
   billing metric. The aggregator only knew Sum / Count / Max / Last, so
   downstream apps were left implementing it with raw SQL.

A fourth pressure was lifecycle. `MeterDefinition.Activated` (a single
boolean) collapsed the three states an admin actually wants — *I'm
drafting this*, *go ahead and ingest*, *stop ingesting but keep the
history* — into one toggle. The admin UI had no good way to author a
meter without it immediately accepting traffic.

ORB and Stripe Billing both expose lifecycle, recompute, and distinct
aggregation as first-class operations; the cost of staying narrower was
that every consumer rebuilt the same workarounds.

## Decision

### 1. Lifecycle managed by `WorkflowLifecycleStatus`

`MeterDefinition` implements `IWorkflowStateful`. The lifecycle
mirrors `Plan` (ADR-017): `Draft → Published → Archived`. Only
`Published` meters accept ingestion; `Draft` lets admins author and
review without polluting production aggregates; `Archived` preserves
history and aggregates but rejects new events.

The legacy `Activated` boolean is kept one release as a computed alias
(`Activated => LifecycleStatus == Published`) and marked `[Obsolete]`.
The deactivate endpoint stays one release as a backend alias for
`POST …/archive`.

### 2. `CountDistinct` aggregation with a typed JSON path

Add `AggregationType.CountDistinct = 4` plus `MeterDefinition.DistinctProperty`
(`string?`). The validator enforces the pairing both ways: `CountDistinct`
requires `DistinctProperty`, every other aggregation rejects it. The
in-memory aggregator extracts the value at the JSON path inside
`MeterEvent.Metadata` and feeds it through a `HashSet<string>` per
aggregate window. Events whose metadata does not contain the property
are excluded from the count (treated as missing — never bucketed as a
synthetic `null` value).

PostgreSQL- or SQL Server-specific JSON aggregation functions were
considered and rejected: distinct-count metrics rarely exceed a few
thousand events per window in B2B SaaS, and the cross-DB constraint
(ADR-024) makes provider-specific operators a maintenance burden.

### 3. Admin recompute / backfill / event-deprecation

Three new endpoints surface the cold-path operations:

- `POST /metering/meters/{id}/recompute` — body
  `{ from: DateTimeOffset, to: DateTimeOffset }`. Window edges snap to
  hourly buckets server-side. The global ingestion watermark is
  **never rewound** (rewinding would force the hot path to re-aggregate
  events past `to` that are already covered by an existing aggregate).
- `POST /metering/events/backfill` — accepts the same shape as the
  standard ingestion endpoint, but the per-event age validator is
  loosened to 365 days. Auto-triggers a recompute on every meter window
  the backfilled events touched.
- `POST /metering/events/{id}/deprecate` — soft-delete with audit trail
  (`DeprecatedAt`, `DeprecationReason`). The aggregator skips deprecated
  events; the auto-recompute on the affected hourly bucket follows.

These three operations all share one critical invariant: nothing
overlaps with concurrent ingestion or with the hourly aggregator job.

### 4. Transaction-scoped advisory lock for cross-job exclusion

`MeteringConcurrencyLock.AcquireAsync(db, meterId, tenantId, ct)` takes
a per-`(meterId, tenantId)` lock that auto-releases at transaction end.
The implementation switches on `db.Database.ProviderName`:

- **PostgreSQL** — `SELECT pg_advisory_xact_lock(hashtext({0}))`
- **SQL Server** — `EXEC sp_getapplock @LockOwner = 'Transaction', @LockTimeout = -1, ...`
- **InMemory / unknown** — no-op (test providers + future providers
  fall back gracefully)

Both the recompute / backfill / deprecate code paths AND the hourly
aggregation job acquire the same lock. They mutually exclude per
`(meter, tenant)` without serializing the global metering pipeline.

The lock is intentionally transaction-scoped (not session-scoped):
crashing mid-recompute releases automatically, and there's no chance of
leaking a held lock into the connection pool.

### 5. SQL ad-hoc metric definitions — DEFER

A separate spike (`docs/dotnet/architecture/spikes/metering-sql-ad-hoc-metrics`)
explored letting admins define ad-hoc metrics as SQL queries against
`MeterEvent`. **Decision: defer** until there are at least three
independent users with a concrete need that the five built-in
aggregations cannot serve. Re-evaluation criterion is documented in the
spike.

## Consequences

### Positive

- **Authorable meters.** Admins can build, review, and validate a meter
  in `Draft` without it consuming production events.
- **Reversible billing data.** Bug-driven over-counts are correctable
  in minutes (deprecate the offending events, run a recompute on the
  affected window) instead of an emergency SQL surgery.
- **First-class active-users.** `CountDistinct` covers the dominant
  per-tenant active-user metric without consumers writing SQL.
- **Cross-DB safety.** The advisory lock pattern works on PostgreSQL
  AND SQL Server with the same call site; tests run against InMemory
  with the no-op fallback.

### Negative

- **One-release deprecation tail.** `Activated` and the deactivate
  endpoint stay one release for backward compatibility — downstream
  apps must migrate during that window.
- **Recompute cost is unbounded.** A 12-month recompute on a busy
  meter scans 12 months of events. The advisory lock holds for the
  duration. Mitigation: window edges snap to hourly buckets, the lock
  is per-`(meter, tenant)` so other tenants are unaffected, and the
  endpoint is gated by `Metering.Meters.Manage` (admin-only).
- **Wider permission surface.** Two new permissions
  (`Metering.Events.Manage`, `Metering.Events.Backfill`) — separated
  from `Metering.Usage.Record` because they're destructive at the
  billing-data level (ISO 27001 A.9.4 — least privilege).

### Non-goals

- **No retroactive lifecycle migration.** Existing `Activated = true`
  meters are mapped to `Published`, `Activated = false` to `Archived`.
  No `PendingReview` state; the workflow stays Draft / Published /
  Archived for parity with `Plan`.
- **No bulk-deprecate UX.** Per-event deprecation is the single tool;
  bulk operations (`deprecate all events for meter X between A and B`)
  belong to a future admin tool.
- **No two-phase commit.** The recompute and backfill operations are
  idempotent within their advisory-lock window; the lock + idempotent
  rebuild eliminates the need for a saga.

## References

- ADR-017 — DDD Aggregate Root vs Entity (lifecycle pattern shared with `Plan`)
- ADR-024 — Cross-database SQL provider strategy
- ADR-032 — `Granit.Catalog.Product` as the shared billing aggregate
  (`MeterDefinition.ProductId` soft reference)
- ADR-036 — Invoicing line item source + product convention
- Spike: `dotnet/architecture/spikes/metering-sql-ad-hoc-metrics` — SQL ad-hoc metric definitions (DEFER)
- ISO 27001 A.9.4 — Least privilege on destructive operations
