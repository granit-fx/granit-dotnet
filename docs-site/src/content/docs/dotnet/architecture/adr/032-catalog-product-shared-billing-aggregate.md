---
title: "ADR-032: Granit.Catalog with Product as the shared billing aggregate"
description: "Introduce a new Granit.Catalog module hosting the Product aggregate, referenced by Metering and Subscriptions via soft (no-FK) ProductId fields. MVP is Host-owned (mirror Plan); the multi-tenant e-commerce path is left open via a documented A/B choice."
sidebar:
  order: 32
  label: "032 - Catalog Product"
---

> **Date:** 2026-04-24
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.Catalog`, `Granit.Catalog.EntityFrameworkCore`,
> `Granit.Catalog.Endpoints`, `Granit.Metering`,
> `Granit.Subscriptions`, `Granit.Invoicing` (future)

## Context

Before this ADR, `Granit.Metering.MeterDefinition` (what is measured) and
`Granit.Subscriptions.PlanPrice` (what is billed) had **no shared point of
attachment to the thing being sold**. The audit chain that ORB calls
`event → metric → item → invoice line` was only loosely held together by
convention-based name matching (the orchestrator looks up meter names that
happen to match plan price labels) — fragile, not queryable in SQL, and
fundamentally untraceable for compliance.

Three pressures converged:

1. **The ORB-alignment audit (EPIC #1155)** surfaced multiple symptoms
   directly tied to this gap:
   - `InvoiceLineItem.SourceId` had no formal contract — it was sometimes
     a meter id, sometimes a plan price id, sometimes free text;
   - cross-module reporting ("how much did we sell of Compute Pack across
     subscriptions and one-shot purchases?") was impossible in SQL;
   - renaming a meter orphaned the historical invoice line label
     semantically.

2. **An explicitly-stated future e-commerce module** (Granit.Catalog
   evolving toward Variants / Stock / Categories / Media / Bundles) had
   nowhere to attach. Each future facet would need a Product to hang off.

3. **External integrations** (Stripe, Avalara, Odoo) were drifting toward
   one mapping table per consuming entity (`PlanExternalMapping`,
   `MeterExternalMapping` next, ...) — multiplying sync jobs and
   reconciliation flows for the same logical "product-in-Stripe".

## Decision

### A new module: `Granit.Catalog`

Three packages mirroring the framework conventions:

- `Granit.Catalog` — domain (`Product`, `ProductType`, `ProductExternalMapping`),
  events, query/export definitions, diagnostics, DI extension
- `Granit.Catalog.EntityFrameworkCore` — isolated `CatalogDbContext`,
  EF configurations, Reader/Writer
- `Granit.Catalog.Endpoints` — minimal API surface (CRUD, lifecycle,
  external mappings, QueryEngine grid)

### Aggregate root: `Product` (not `Item`, not `BillableItem`)

The naming was the most contested decision. Surveyed terminology:

- **E-commerce** (10/10): Shopify, Magento, commercetools, Stripe,
  Zuora, WooCommerce, MedusaJS, BigCommerce, Shopware, Salesforce CC →
  all use `Product`
- **ERP / accounting**: NetSuite, Quickbooks, Xero, SAP B1 → use `Item`
- **Modern subscription billing**: Stripe, Zuora → `Product`;
  ORB, Chargebee 2.0 → `Item`

**Choice: `Product`**. Rationale:

- Dominant in e-commerce — natural fit for the announced future direction
- Stripe (already in Granit's module tree via `Granit.Payments.Stripe`)
  uses `Product`; aligning eases external-mapping mental model
- Recognizable to non-technical roles (sales, marketing, support) —
  `Item` evokes "line item" (`InvoiceLineItem`) which we already have,
  creating confusion in billing contexts
- Future composition (`ProductVariant`, `ProductCategory`, `ProductMedia`,
  `ProductBundle`, `ProductReview`) all read naturally; `ItemVariant` /
  `ItemCategory` sound ERP-ish

Rejected: `CatalogItem` (too generic), `BillableItem` (too narrow for
e-commerce), `Sku` (too narrow — SKU is a *variant* identifier in
mature schemas).

### Lifecycle: reuse `WorkflowLifecycleStatus`

`Product : IWorkflowStateful` mirrors `Plan` exactly — `Draft`,
`Published`, `Archived` (skipping `PendingReview` for MVP). No new state
machine; consumers benefit from the existing `Granit.Workflow`
infrastructure (status-change auditing, transition records).

### Cross-module references: soft (no SQL FK)

`MeterDefinition.ProductId` (Guid?, nullable) and
`PlanPrice.ProductId` (Guid?, nullable) are **soft references**. The
catalog deletion safety check (refuse to delete a product if anything
references it) is left to the application layer because:

- Granit modules each own an isolated `DbContext` — cross-context
  FKs are rejected by EF Core
- A soft reference makes the dependency cycle one-way only (`Metering`
  and `Subscriptions` depend on the *shape* of a Product id, not on the
  Catalog module itself); cleaner module boundaries
- The reference being optional (nullable) keeps the change additive
  for downstream apps that don't care yet

### MVP scope: Host-owned, no `IMultiTenant`

`Product` does **not** implement `IMultiTenant`. It mirrors `Plan`:
products are global to the SaaS provider (the "Host"), and tenants
reference them through their subscriptions. SKU uniqueness is enforced
globally (`UNIQUE (Sku)` on the catalog table).

Why not multi-tenant from day 1: the framework's current
`IMultiTenant` query filter is **strict equality**
(`entity.TenantId == currentTenant.Id`) — it would *exclude* Host-owned
rows from tenant queries, breaking the "tenant browses Host catalog"
use case fundamental to billing. Fixing the filter today would lock us
into a design before the e-commerce phase has been cadred. See
"Path forward" below.

### Free-form metadata: `IHasMetadata`, not a new dictionary

`Product : IHasMetadata` reuses the framework's existing
extension surface (`GetMetadataValue`, `SetMetadataValue`,
`MetadataSyncInterceptor`, `MapProperty<T>` for SQL promotion).

A first iteration introduced a `Dictionary<string, string> Metadata`
property; on review it was rejected because Granit already standardizes
this concept via `IHasMetadata` (`GranitUser`, `OpenIddict`,
others use it). Two parallel patterns for the same concept = exactly the
sort of inconsistency the framework convention exists to prevent. A
separate tech-debt issue (#1183) tracks a possible framework-wide rename
of the convention to `Metadata` (the industry-standard term used by
Stripe, ORB, AWS, Kubernetes, Shopify) — independent of this ADR.

## Alternatives considered

### Place `Product` inside `Granit.Subscriptions` or `Granit.Metering`

Rejected. Either choice creates a circular conceptual dependency
(Product belongs to neither) and forces the other module to acquire a
hard reference (project ref) it doesn't need. A dedicated module
respects boundaries.

### Stay event-driven without an explicit Product

Rejected. The current convention-based matching (`UsageSummaryReadyEto`
carries `MeterName`; orchestrator string-matches against a plan price
description) is fragile, untraceable, and will only get worse as the
catalog grows past 10 sellable things.

### Multi-tenant from day 1 with custom dual-visibility filter

Considered but deferred. Would need a new framework-level filter primitive
(`tenantMatch || isHostOwned`). Locks the design before the e-commerce
phase has produced concrete requirements. Better to ship Host-owned MVP
now and choose the multi-tenant strategy when actual e-commerce code is
on the horizon (see next section).

## Path forward (multi-tenant e-commerce)

When Granit grows toward Tenant → Customer e-commerce inventory, two
paths remain open:

- **(A) Extend `Product` with `IMultiTenant`** plus a custom
  dual-visibility filter `(TenantId IS NULL OR TenantId = @current)`.
  Pros: single aggregate, no parallel API. Cons: requires a new
  framework filter primitive; backward-compatibility care for existing
  apps querying products today.

- **(B) Sibling `MerchantProduct : IMultiTenant`** in the same module.
  Pros: clean separation between Host-owned billing catalog and
  Tenant-owned e-commerce inventory; no framework changes. Cons: two
  aggregates with overlapping fields; future code paths split between
  them.

The choice will be made when e-commerce requirements are concrete.
Today's design forecloses neither.

## Consequences

**Positive:**

- ORB-style invoice line item provenance becomes a SQL join, not
  a name-matching convention
- `InvoiceLineItem.SourceType + SourceId` gets a documented contract
  (Phase 5 of EPIC #1155)
- External integrations (Stripe, Avalara, Odoo) sync against one
  product-level record instead of N per-entity mappings
- Cross-module reporting ("how much of Compute Pack across all sales
  channels?") becomes a one-line query
- Future e-commerce module has a domain home

**Negative:**

- One more module to maintain (3 packages, ~20 files of scaffolding,
  36 localization files for 18 cultures × 2 modules)
- Soft references mean the application layer must handle catalog
  deletion safety
- Two-step authoring (create Product → create Plan referencing it)
  adds a small UX friction for SaaS apps that previously authored
  plans in isolation; mitigated by allowing `ProductId` to be null
  (backward compatible)

**Neutral:**

- No EF migration shipped with the framework (per existing convention
  — consuming apps run `dotnet ef migrations add` themselves);
  documented in the cross-module wiring commits (#1163, #1164)

## References

- EPIC #1155 — ORB alignment programme
- Feature #1157 — Phase 1 (Catalog scaffold)
- Stories #1162 (scaffold), #1163 (Metering wiring), #1164 (Subscriptions wiring)
- Tech-debt #1183 — proposed framework-wide `Metadata → Metadata` rename
- ORB documentation — <https://docs.withorb.com>
- Stripe Products API — <https://docs.stripe.com/api/products>
