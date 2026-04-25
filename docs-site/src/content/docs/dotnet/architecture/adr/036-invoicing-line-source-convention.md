---
title: "ADR-036: Invoicing line item source + product convention"
description: "Formalize InvoiceLineItem.SourceType → SourceId as a typed audit chain (Guid for Usage/Subscription) and add an optional ProductId carried from MeterDefinition.ProductId / PlanPrice.ProductId so invoice lines stay attributable to a Granit.Catalog.Product across renames and price versioning."
sidebar:
  order: 36
  label: "036 - Invoicing Line Source"
---

> **Date:** 2026-04-25
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.Invoicing`, `Granit.Invoicing.Abstractions`,
> `Granit.Invoicing.EntityFrameworkCore`, `Granit.Subscriptions`
> (orchestrators), `Granit.Metering` (UsageSummaryReadyEto)

## Context

`Granit.Invoicing.InvoiceLineItem` already carried a `SourceType`
(`Subscription` / `Usage` / `OneShot` / `Credit`) and an optional `SourceId`
(`string?`). In practice the field had no formal contract:

- The `Subscription` orchestrators stored the **subscription id** as a Guid
  string when generating recurring lines, but nothing prevented a future
  caller from putting a plan price id, an external Stripe id, or free text.
- The `Usage` lines emitted by `DefaultUsageInvoiceOrchestrator` stored the
  **meter definition id** — again as a Guid string by convention only.
- One-shot e-commerce purchases and manual credit lines were free-form by
  design (SKU codes, refund references, etc.).

Two consequences:

1. **Audit chain was broken in practice.** ADR-032 declared the
   `event → metric → product → invoice line` chain as a goal, but with no
   typed contract on `(SourceType, SourceId)` the only way to verify the
   chain in a SQL query was to assume the convention held and pray. ISO
   27001 A.12.4 (logging and monitoring) wants this kind of traceability
   to be enforced, not implied.
2. **Product attribution was lost across renames.** When a meter was
   renamed (`apicall_count` → `compute_units`) or a `PlanPrice` was
   replaced by a newer version, the historical invoice lines kept their
   stale `Description` text. There was no stable identifier on the line
   pointing back to "the thing that was sold" — exactly the gap that
   `Granit.Catalog.Product` was created to close (ADR-032).

Phase 5 of EPIC #1155 (ORB-alignment) calls for closing both gaps in a
single small change: a typed convention on `(SourceType, SourceId)` and a
soft propagation of `ProductId` from the upstream entity.

## Decision

### 1. Typed convention on `(SourceType, SourceId)`

`SourceId` is no longer free-form for the two billing source types. The
factory `InvoiceLineItem.Create` enforces the contract at the entity
boundary:

| `SourceType` | `SourceId` contract |
| ------------ | ------------------- |
| `Usage` | **Required**. MUST be `MeterDefinition.Id` as a Guid string. |
| `Subscription` | **Required**. MUST be `Subscription.Id` (or `PlanPrice.Id`) as a Guid string. |
| `OneShot` | **Free-form**. May be a SKU, an external id, or `null`. |
| `Credit` | **Free-form**. May be a refund reference, a credit memo number, or `null`. |

Failures throw `ArgumentException` synchronously — there is no in-flight
validation framework hop. `OneShot` and `Credit` stay free-form because
they cover heterogeneous data sources (e-commerce SKUs, manual
adjustments, third-party refund ids) where requiring a Guid would be
incorrect.

### 2. Optional `ProductId` on the line item

`InvoiceLineItem.ProductId` (`Guid?`) is added as a **soft** reference to
`Granit.Catalog.Product`. The same soft-reference rules from ADR-032
apply: no SQL FK, no cross-module DbContext join, no integrity
enforcement at the EF layer. A non-clustered index supports
"show me all invoice lines for product X" reporting queries.

The orchestrators populate the field when they have it:

- `DefaultUsageInvoiceOrchestrator` reads it from the `UsageSummaryReadyEto`,
  which the metering-side `BillingCycleUsagePublisher` populates from
  `MeterDefinition.ProductId`.
- `DefaultBillingCycleInvoiceOrchestrator` resolves it from the active
  `PlanPrice` for the subscription's `(currency, interval)` slot — or
  from the override price pinned by an active `SubscriptionPhase` when
  one applies.

When the upstream entity has no `ProductId` (no `Granit.Catalog`
installation, or a meter / price not yet linked to a product), the field
stays `null`. The change is fully additive.

### 3. `UsageSummaryReadyEto` carries `MeterProductId`

The contract change to the integration event is additive (new optional
property, defaults to `null`). Existing publishers that don't know about
`Granit.Catalog` continue to publish events without it; existing
subscribers that don't read it continue to work unchanged.

## Consequences

### Positive

- **Audit chain is enforceable in SQL.** A reviewer can write
  `SELECT * FROM invoice_line_items WHERE source_type = 'Usage' AND source_id = '<meter_id>'`
  and trust that every matching row genuinely came from that meter — the
  factory rejects deviations.
- **Stable product attribution.** Renaming a meter or replacing a plan
  price no longer orphans historical invoice lines; the `ProductId`
  column points to a `Granit.Catalog.Product` whose own lifecycle
  (Draft / Published / Archived) is independent.
- **Cross-cutting reporting.** "Revenue per product across subscriptions
  and usage" becomes a single `GROUP BY product_id` — previously
  impossible without joining stale name strings.
- **Index-supported lookups.** The non-clustered index on `ProductId`
  makes per-product invoice-line queries efficient even on multi-tenant
  databases.

### Negative

- **A small breaking change at the entity boundary** for callers that
  passed a non-Guid `SourceId` for `Usage` / `Subscription`. The
  framework's own orchestrators always passed Guids, so this is a
  formalization rather than a behavioral break — but downstream apps
  with custom orchestrators that violated the convention will now fail
  fast at construction. This is intentional: failing fast at the
  factory is preferable to silently storing untraceable lines.
- **The new optional field on `UsageSummaryReadyEto`** widens the public
  contract of `Granit.Metering.Abstractions`. Wolverine's serializer
  handles the additional nullable property transparently in both
  directions; the change is wire-compatible with older versions of the
  consumer / publisher.

### Non-goals

- **No FK enforcement on `ProductId`**. Cross-module FKs are explicitly
  rejected by ADR-032; the catalog deletion safety check stays in the
  application layer.
- **No retroactive backfill**. Historical invoice lines created before
  this change keep `ProductId = NULL`. A backfill migration is left to
  consuming applications that want it.
- **No tightening of `OneShot` / `Credit`**. Both stay free-form. Any
  future tightening would belong in a separate ADR with concrete use
  cases on the table.

## References

- ADR-032 — Granit.Catalog with Product as the shared billing aggregate
- EPIC #1155 — ORB-alignment program (Phase 5)
- ISO 27001 A.12.4 — Logging and monitoring
