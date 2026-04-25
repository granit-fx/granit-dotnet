---
title: "ADR-034: Subscriptions — pricing tiers, phases, discounts, price overrides"
description: "Add four child entities to Subscription/PlanPrice — PricingTier (volume / graduated brackets), SubscriptionPhase (scheduled plan changes with optional override price + discount), SubscriptionDiscount (cumulative reductions), SubscriptionPriceOverride (per-customer negotiated amounts) — to support usage-based billing patterns without exposing them as HTTP endpoints in the MVP."
sidebar:
  order: 34
  label: "034 - Subscriptions Phases & Tiered"
---

> **Date:** 2026-04-25
> **Authors:** Jean-Francois Meyers
> **Scope:** `Granit.Subscriptions`, `Granit.Subscriptions.EntityFrameworkCore`,
> `Granit.Subscriptions.Endpoints` (orchestrators only — no new HTTP surface yet)

## Context

Pre-ADR, `Granit.Subscriptions` modeled a flat `(Plan, PlanPrice,
Subscription)` triple: one subscription points at one plan with one
active price, and the billing-cycle orchestrator multiplies quantity by
unit price. That's enough for vanilla SaaS (Stripe Billing — Subscription
plus Standard pricing), but four real workflows kept slipping through:

1. **Tiered pricing.** Once a plan crosses "$0.10 per call up to 10K,
   then $0.05 above 10K", the orchestrator can't compute the invoice
   from a single unit price. Both the Volume model (every unit at the
   bracket's rate) and the Graduated model (each bracket charged at its
   own rate) are common; ORB exposes them as first-class.
2. **Scheduled plan changes.** Customer Success negotiates a
   downgrade-at-renewal or a 90-day promotional plan. The orchestrator
   needs to know "which plan is active *for this billing cycle*" and
   apply the right one — but the existing model only carried the
   subscription's current plan id.
3. **Negotiated discounts.** Sales gives a key account 20% off for
   12 months. Today this requires duplicating the plan price or
   surgically editing the invoice — neither is auditable.
4. **Per-customer price overrides.** Distinct from a discount: the
   subscription points at a specific `PlanPriceOverride` row that
   replaces the catalog price for this one customer. Used for
   grandfathered enterprise terms.

Three of these four (tiers, phases, discounts) are billing-engine
primitives — they live in the domain regardless of whether admins ever
edit them through HTTP. The fourth (price override) is admin-driven and
needs an endpoint eventually, but not in this iteration.

## Decision

### 1. Four new child entities (Entity, no `AggregateRoot`)

| Entity | Parent | Cardinality |
| ------ | ------ | ----------- |
| `PricingTier` | `PlanPrice` | 1..n (zero = flat pricing) |
| `SubscriptionPhase` | `Subscription` | 0..n (zero = single-phase, current model) |
| `SubscriptionDiscount` | `Subscription` | 0..n (cumulative) |
| `SubscriptionPriceOverride` | `Subscription` | 0..1 (per active phase) |

All four are domain entities, mutable through behavior methods on the
parent aggregate, never instantiated from outside. They don't carry their
own state machine (`SubscriptionPhase` doesn't have `Draft → Active →
Expired`; activeness is computed from `(StartDate, EndDate)` against
`clock.Now`).

### 2. `PricingTier` — Volume vs Graduated

```csharp
public sealed class PricingTier : Entity
{
    public decimal? UpToQuantity { get; }  // null = open-ended (last tier)
    public decimal UnitAmount { get; }
    public decimal? FlatAmount { get; }    // optional package fee on top of UnitAmount * Qty
    public int SortOrder { get; }
}

public enum TieringMode { Volume, Graduated }
```

`PlanPrice.Tiers` (read-only collection) + `PlanPrice.TieringMode`
(`TieringMode?`). The mode is null when the price is flat
(`Tiers.Count == 0`); the validator on the create endpoint enforces the
pairing. The bracket sequence is validated at insertion: ascending
`UpToQuantity`, exactly one open-ended tier (`UpToQuantity = null`)
which must be the last.

`TieredPricingCalculator.Compute(quantity, mode, tiers)` is the single
math entry point — both `DefaultUsageInvoiceOrchestrator` and any
future ad-hoc preview UI go through it. Volume math walks the brackets
to find the one containing `quantity` and applies its rate to the full
quantity; Graduated walks every bracket and sums the partial usages.

### 3. `SubscriptionPhase` — half-open intervals + active resolution

```csharp
public sealed class SubscriptionPhase : Entity
{
    public DateTimeOffset StartDate { get; }
    public DateTimeOffset? EndDate { get; }   // null = open-ended
    public PlanId PlanId { get; }
    public Guid? OverridePriceId { get; }     // pin a specific PlanPrice
    public decimal? DiscountPercent { get; }  // 0–100; phase-flat discount
    public bool Covers(DateTimeOffset instant);
}
```

Subscriptions resolve the active phase via
`subscription.GetActivePhase(clock.Now)`. Half-open semantics: `[Start, End)`
— end-exclusive so back-to-back phases don't double-count an instant.
When no phase covers `now`, the orchestrator falls back to
`subscription.PlanId` / `PlanPriceId` (single-phase = backward
compatible with pre-Phase-3 behavior).

The phase's optional `OverridePriceId` lets sales pin a grandfathered
price for a phase without touching the global plan price catalog. The
optional `DiscountPercent` is a phase-flat reduction applied
multiplicatively to the resolved base price.

### 4. `SubscriptionDiscount` — cumulative stacking

```csharp
public sealed class SubscriptionDiscount : Entity
{
    public DiscountType Type { get; }   // Percentage | FixedAmount | Trial
    public decimal Value { get; }
    public DateTimeOffset? StartsAt { get; }
    public DateTimeOffset? ExpiresAt { get; }
}
```

Multiple active discounts cumulate via
`SubscriptionDiscountCalculator.Apply(basePrice, discounts, clock)`.
Cumulation order: Trial first (zeroes the bill), then Percentage
discounts (combined multiplicatively to avoid the order-dependence of
sequential subtraction), then FixedAmount discounts. Banker's rounding
to four decimals after each step. Negative result is clamped to zero —
discounts can't generate a refund.

### 5. `SubscriptionPriceOverride` — per-customer negotiated amount

```csharp
public sealed class SubscriptionPriceOverride : Entity
{
    public Guid PlanPriceId { get; }   // the plan price being overridden
    public decimal Amount { get; }     // negotiated amount in PlanPrice.Currency
}
```

When an active phase pins an `OverridePriceId` AND the subscription has
a matching override, the orchestrator uses the override's `Amount`
instead of the plan price's catalog amount. The override is
single-currency by construction (matches the plan price's currency);
multi-currency negotiations require a separate override per currency.

### 6. NO HTTP endpoints in this iteration — orchestrator-only

All four entities are populated by the orchestrator at billing time
from existing data; none of them have admin-side `POST` endpoints in
this PR. Rationale:

- Tiers and phases are typically authored by configuration tooling
  (Stripe sync, internal admin scripts), not click-by-click in a UI.
- Discounts and overrides need a richer authoring UX (customer search,
  approval workflow, audit trail) than this PR is scoped for.
- Shipping the domain primitives without HTTP keeps the MVP small and
  lets the orchestrator behavior stabilize in production before the
  endpoint surface is locked.

A follow-up ADR will spec the HTTP surface.

## Consequences

### Positive

- **Tiered pricing works end-to-end.** A plan with `PricingModel.Tiered`
  combined with a populated `Tiers` collection produces correct invoices
  in both Volume and Graduated modes — closes a recurring user request.
- **Scheduled plan changes are first-class.** Phase resolution lives
  in the domain, not the orchestrator's caller — every billing-cycle
  computation goes through the same path.
- **Discount stacking is deterministic.** The cumulation order is fixed
  (Trial → Percentage cumulated multiplicatively → FixedAmount), so
  the same set of active discounts always produces the same final
  amount regardless of insertion order.
- **No HTTP yet means no API breaking change.** Apps that don't use
  these primitives are unaffected; apps that want them populate the
  child collections through their own data path.

### Negative

- **Orchestrator complexity grew.** `DefaultBillingCycleInvoiceOrchestrator`
  now does phase resolution + price override + discount stacking +
  optional tier math. Compensated by extracting
  `TieredPricingCalculator` and `SubscriptionDiscountCalculator` as
  pure functions with their own test suites.
- **Domain entities without endpoints surprise some readers.**
  Documented in `saas/subscriptions.mdx` that the entities are
  populated through configuration tooling for now.
- **The `Tiered` enum in `PricingModel` only matters when `Tiers`
  is non-empty.** Plans with `PricingModel.Tiered` and no tiers fall
  back to per-unit pricing. This is intentional (avoids an invariant
  during plan creation) but documented.

### Non-goals

- **No usage-based discount stacking.** Discounts apply to base price
  per phase, not per usage line. Mixing the two opens credit-stacking
  fraud surfaces; revisit when there's a concrete use case.
- **No multi-currency overrides.** A `SubscriptionPriceOverride` is
  scoped to one `PlanPriceId` (one currency). Multi-currency
  negotiations require multiple overrides.
- **No retroactive phase activation.** Phases are forward-looking;
  past billing cycles aren't re-billed if a phase is added with a
  past `StartDate`.
- **No discount on top of tiered usage.** When a discount is active
  on a tiered plan, the discount applies to the base subscription
  charge only — usage lines stay at their bracketed rate.

## References

- ADR-017 — DDD Aggregate Root vs Entity (PricingTier, SubscriptionPhase,
  SubscriptionDiscount, SubscriptionPriceOverride are entities)
- ADR-032 — `Granit.Catalog.Product` (PlanPrice.ProductId reference)
- ADR-036 — Invoicing line item source + product convention
  (orchestrator populates `productId` on line items from the resolved
  PlanPrice)
- Stripe Billing — pricing tiers documentation (Volume vs Graduated)
- ORB — schedule + price override semantics (alignment target)
